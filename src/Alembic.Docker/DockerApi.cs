using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alembic.Common.Contracts;
using Alembic.Common.Services;
using Microsoft.Extensions.Logging;

namespace Alembic.Docker
{
    public sealed class DockerApi : IDockerApi
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);
        private static readonly Action<object?> Callback = delegate (object? stream)
        {
            if (stream == null)
                return;

            ((IDisposable)stream).Dispose();
        };

        private static readonly Func<JsonSerializerOptions> GetSerializationOptions = delegate ()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        };

        private readonly IDockerClient _client;
        private readonly IReporter _reporter;
        private readonly ILogger _logger;
        private readonly IContainerRetryTracker _retryTracker;

        public DockerApi(IDockerClient client, IReporter reporter, IContainerRetryTracker retryTracker, ILogger<DockerApi> logger)
        {
            _client = client;
            _reporter = reporter;
            _retryTracker = retryTracker;
            _logger = logger;
        }

        public async Task<string> Ping(CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) =
                await
                    _client.MakeRequestAsync(
                        HttpMethod.Get,
                        "_ping",
                        null,
                        null,
                        Timeout,
                        cancellation);

            if (status == HttpStatusCode.OK)
                return body;

            throw new DockerApiException(status, "Unable to ping Docker server");
        }

        public async Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) =
                await
                    _client.MakeRequestAsync(
                        HttpMethod.Get,
                        "containers/json",
                        "all=true",
                        null,
                        Timeout,
                        cancellation);

            if (status != HttpStatusCode.OK)
                return Enumerable.Empty<ContainerInfo>();

            var containers = JsonSerializer.Deserialize<ContainerInfo[]>(body);

            return containers;
        }

        public async Task<Container> InspectContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) =
                await
                    _client.MakeRequestAsync(
                        HttpMethod.Get,
                        $"containers/{id}/json",
                        null,
                        null,
                        Timeout,
                        cancellation);

            if (status == HttpStatusCode.OK)
            {
                var container = JsonSerializer.Deserialize<Container>(body, GetSerializationOptions());

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
            var container = await InspectContainer(id, cancellation);

            if (container == null)
                return HttpStatusCode.NotFound;

            (HttpStatusCode status, string body) =
                await
                    _client.MakeRequestAsync(
                        HttpMethod.Post,
                        $"containers/{id}/kill",
                        null,
                        null,
                        Timeout,
                        cancellation);

            if (status == HttpStatusCode.NoContent)
            {
                await _reporter.Send(Report.CreateKill("Container killed successfully.", container), cancellation);

                _logger.LogInformation($"Container: {id} killed successfully.");

                _retryTracker.Remove(id);
            }
            else
            {
                await _reporter.Send(Report.CreateRestart("Failed to kill container. Response status: {status}", container), cancellation);

                _logger.LogWarning($"Failed to kill container: {id}. Response status: {status} content: {body}");
            }

            return status;
        }

        public async Task MonitorHealthStatus(
            CancellationToken cancellation,
            int restartCount = 3,
            bool killUnhealthyContainer = true,
            Action<UnhealthyStatusActionReport> onUnheathyStatusReceived = null)
        {
            var stream =
                await
                    _client.MakeRequestForStreamAsync(
                        HttpMethod.Get,
                        "events",
                        @"filters=%7B%22event%22%3A%7B%22health_status%22%3Atrue%7D%7D",
                        null,
                        Timeout,
                        cancellation);

            using (cancellation.Register(Callback, stream, false))
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false));

                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    var containerHealth = JsonSerializer.Deserialize<ContainerInfo>(line, GetSerializationOptions());

                    var report = new UnhealthyStatusActionReport(0, false, false);

                    if (containerHealth.Status.Split(":")[1].Trim().ToLowerInvariant() != "unhealthy")
                        continue;

                    _retryTracker.Add(containerHealth.Id);
                    report.RestartCount = _retryTracker.GetRetryCount(containerHealth.Id);

                    if (report.RestartCount <= restartCount)
                    {
                        var restartStatus = await RestartContainer(
                            containerHealth.Id,
                            $"Container restart number: {report.RestartCount} of {restartCount}",
                            cancellation);

                        report.Restarted = restartStatus == HttpStatusCode.NoContent;

                        onUnheathyStatusReceived?.Invoke(report);

                        continue;
                    }

                    if (!killUnhealthyContainer)
                    {
                        onUnheathyStatusReceived?.Invoke(report);

                        continue;
                    }

                    var killStatus = await KillContainer(containerHealth.Id, cancellation);
                    report.Killed = killStatus == HttpStatusCode.NoContent;

                    onUnheathyStatusReceived?.Invoke(report);
                }
            }
        }

        private async Task<HttpStatusCode> RestartContainer(string id, string reportMessage, CancellationToken cancellation)
        {
            var container = await InspectContainer(id, cancellation);

            if (container == null)
                return HttpStatusCode.NotFound;

            (HttpStatusCode status, string body) =
                await
                    _client.MakeRequestAsync(
                        HttpMethod.Post,
                        $"containers/{id}/restart",
                        null,
                        null,
                        Timeout,
                        cancellation);

            if (status == HttpStatusCode.NoContent)
                _logger.LogInformation($"Container: {id} restarted successfully.");
            else
                _logger.LogWarning($"Failed to restart container: {id}. Response status: {status} content: {body}");

            var slackMessage =
                status == HttpStatusCode.NoContent
                    ? reportMessage
                    : $"Failed to restart container. Response status: {status}";

            await _reporter.Send(Report.CreateRestart(slackMessage, container), cancellation);

            return status;
        }
    }
}