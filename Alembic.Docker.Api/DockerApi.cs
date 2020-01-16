using Alembic.Docker.Contracts;
using Alembic.Docker.Services;
using Alembic.Reporting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Alembic.Docker.Api.DockerClient;

namespace Alembic.Docker.Api
{
    public class DockerApi : IDockerApi
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
        private static readonly IEnumerable<ApiResponseErrorHandlingDelegate> NoErrorHandlers = Enumerable.Empty<ApiResponseErrorHandlingDelegate>();

        private ConcurrentDictionary<string, int> _containerRetries = new ConcurrentDictionary<string, int>();

        private readonly IDockerClient _client;
        private readonly IReporter _reporter;
        private readonly ILogger _logger;

        public DockerApi(IDockerClient client, IReporter reporter, ILogger<DockerApi> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> Ping(CancellationToken cancellation)
        {
            (HttpStatusCode code, string body) = await _client.MakeRequestAsync(NoErrorHandlers, HttpMethod.Get, "_ping", null, null, Timeout, cancellation);

            if (code == HttpStatusCode.OK)
                return body;

            throw new DockerApiException(code, "Unable to ping Docker server");
        }

        public async Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(NoErrorHandlers, HttpMethod.Get, "containers/json", "all=true", null, Timeout, cancellation);

            if (status == HttpStatusCode.OK)
            {
                var containers = JsonConvert.DeserializeObject<ContainerInfo[]>(body);

                return containers;
            }

            return Enumerable.Empty<ContainerInfo>();
        }

        public async Task<Container> InspectContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(NoErrorHandlers, HttpMethod.Get, $"containers/{id}/json", null, null, Timeout, cancellation);

            if (status == HttpStatusCode.OK)
            {
                var container = JsonConvert.DeserializeObject<Container>(body);

                return container;
            }

            if (status == HttpStatusCode.NotFound)
                return null;

            return null;
        }

        public Task<HttpStatusCode> RestartContainer(string id, CancellationToken cancellation)
        {
            return RestartContainer(id, $"Container: {id} restarted.", cancellation);
        }

        public async Task<HttpStatusCode> KillContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(NoErrorHandlers, HttpMethod.Post, $"containers/{id}/kill ", null, null, Timeout, cancellation);

            var container = await InspectContainer(id, cancellation);

            if (status == HttpStatusCode.NoContent)
            {
                await _reporter.Send(CreateSlackMessage("Kill", "danger", "Container killed successfully.", DateTime.UtcNow, container), cancellation);

                if (!_containerRetries.TryRemove(id, out int _))
                    _logger.LogError($"Failed to remove container: {id} from the cache.");

                _logger.LogInformation($"Container: {id} killed successfully.");
            }
            else
            {
                await _reporter.Send(CreateSlackMessage("Kill", "danger", "Failed to kill container. Response status: {status}", DateTime.UtcNow, container), cancellation);
                _logger.LogWarning($"Failed to kill container: {id}. Response status: {status} content: {body}");
            }

            return status;
        }

        public async Task MonitorHealthStatus(CancellationToken cancellation, int restartCount = 3, bool killUnhealthyContainer = true)
        {
            var stream = await _client.MakeRequestForStreamAsync(NoErrorHandlers, HttpMethod.Get, "events", @"filters=%7B%22event%22%3A%7B%22health_status%22%3Atrue%7D%7D", null, Timeout, cancellation);

            using (cancellation.Register(Callback, stream, false))
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false));

                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var containerHealth = JsonConvert.DeserializeObject<ContainerInfo>(line);

                    if (containerHealth.Status.Split(":")[1].Trim() == "unhealthy")
                    {
                        if (_containerRetries.TryGetValue(containerHealth.Id, out var _))
                            _containerRetries[containerHealth.Id] = _containerRetries[containerHealth.Id] + 1;
                        else
                            _containerRetries[containerHealth.Id] = 1;

                        if (_containerRetries[containerHealth.Id] > restartCount)
                        {
                            if (!killUnhealthyContainer)
                                continue;

                            await KillContainer(containerHealth.Id, cancellation);
                            continue;
                        }

                        await RestartContainer(containerHealth.Id, $"Container restart number: {_containerRetries[containerHealth.Id]} of {restartCount}", cancellation);
                    }
                }
            }
        }

        private async Task<HttpStatusCode> RestartContainer(string id, string reportMessage, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(NoErrorHandlers, HttpMethod.Post, $"containers/{id}/restart ", null, null, Timeout, cancellation);
            var container = await InspectContainer(id, cancellation);

            object slackMessage = status == HttpStatusCode.NoContent
                ? CreateSlackMessage("Restart", "warning", reportMessage, DateTime.UtcNow, container)
                : CreateSlackMessage("Restart", "warning", $"Failed to restart container. Response status: {status}", DateTime.UtcNow, container);

            await _reporter.Send(slackMessage, cancellation);

            if (status == HttpStatusCode.NoContent)
                _logger.LogInformation($"Contrainer: {id} restarted successfully.");
            else
                _logger.LogWarning($"Failed to restart container: {id}. Response status: {status} body: {body}");

            return status;
        }

        private object CreateSlackMessage(string eventName, string color, string message, DateTime date, Container container)
        {
            var fields = new List<object>
            {
                new { title = "Container id", value = $"`{container.Id}`"},
                new { title = "Container number", value = $"`{container.ExtractContainerNumberLabelValue()}`"},
                new { title = "Service", value = $"`{container.ExtractServiceLabelValue()}`"},
                new { title = "Image", value = $"`{container.Image}`" },
                new { title = "Status", value = $"`{container.State.Status}`"},
                new { title = "Exit code", value = $"`{container.State.ExitCode}`" },
                new { title = "Logs"},
            };

            foreach (var log in container.State.Health.Logs)
            {
                fields.Add(new { value = $"`{JsonConvert.SerializeObject(log)}`\n" });
            }

            var content =
                new
                {
                    attachments = new object[]
                    {
                        new
                        {
                            mrkdwn_in = new[] { "text" },
                            color = color,
                            pretext = $"*Event:* {eventName}",
                            text = $"_{message}_",
                            fields = fields,
                            footer = "Date:",
                            ts = UtcNowToUnixTimestamp(date)
                        },
                    }
                };

            return content;
        }

        private static double UtcNowToUnixTimestamp(DateTime date)
        {
            TimeSpan difference = date.ToUniversalTime() - DateTime.UnixEpoch;

            return Math.Floor(difference.TotalSeconds);
        }

        private static readonly Action<object?> Callback = delegate (object? stream)
        {
            ((IDisposable)stream).Dispose();
        };
    }
}