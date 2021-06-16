using Alembic.Common.Contracts;
using Alembic.Common.Resiliency;
using Alembic.Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Reporting.Slack
{
    public class WebHookReporter : IReporter
    {
        private static readonly Uri SLACK_WEBHOOK_URI = new Uri("https://hooks.slack.com/");

        private static readonly Func<HttpResponseMessage, bool> TransientHttpStatusCodePredicate = delegate (HttpResponseMessage response)
        {
            if (response.StatusCode < HttpStatusCode.InternalServerError)
                return response.StatusCode == HttpStatusCode.RequestTimeout;

            return true;
        };

        private readonly WebHookReporterOptions _options;
        private readonly HttpClient _client;
        private readonly IRetryProvider _retryProvider;
        private readonly ILogger _logger;

        public WebHookReporter(IOptionsMonitor<WebHookReporterOptions> options, IHttpClientFactory factory, IRetryProvider retryProvider, ILogger<WebHookReporter> logger)
        {
            _options = options.CurrentValue;
            _client = factory.CreateClient();
            _client.BaseAddress = SLACK_WEBHOOK_URI;
            _retryProvider = retryProvider;
            _logger = logger;
        }

        public async Task Send(Report report, CancellationToken cancellation)
        {
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellation);

            var message =
                report.Operation == ContainerOperation.Restart
                ? CreateRestartMessage(report.Message, report.Container)
                : CreateKillMesage(report.Message, report.Container);

            var payload = JsonSerializer.Serialize(message);

            try
            {
                await
                    _retryProvider.RetryOn<HttpRequestException, HttpResponseMessage>(
                        CheckError,
                        TransientHttpStatusCodePredicate,
                        () => Send(_client, _options, payload, _logger, linkedSource.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report sending failed");
            }
        }

        private static object CreateKillMesage(string message, Container container)
        {
            return CreateSlackMessage("Kill", "danger", message, DateTime.UtcNow, container);
        }

        private static object CreateRestartMessage(string message, Container container)
        {
            return CreateSlackMessage("Restart", "warning", message, DateTime.UtcNow, container);
        }

        private static object CreateSlackMessage(string eventName, string color, string message, DateTime date, Container container)
        {
            var fields = new List<object>
            {
                new { title = "Container id", value = $"`{container.Id}`"},
                new { title = "Container number", value = $"`{container.Config.Labels.ExtractContainerNumberLabelValue()}`"},
                new { title = "Service", value = $"`{container.Config.Labels.ExtractServiceLabelValue()}`"},
                new { title = "Image", value = $"`{container.Image}`" },
                new { title = "Status", value = $"`{container.State.Status}`"},
                new { title = "Exit code", value = $"`{container.State.ExitCode}`" },
                new { title = "Logs"},
            };

            foreach (var log in container.State?.Health?.Log ?? Enumerable.Empty<HealthLog>())
            {
                fields.Add(new { value = $"`{JsonSerializer.Serialize(log)}`\n" });
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

        private bool CheckError(HttpRequestException x)
        {
            _logger.LogError(x, "Report sending failed");

            if (!x.Data.Contains(nameof(HttpStatusCode)))
                return false;

            var statusCode = (HttpStatusCode)x.Data[nameof(HttpStatusCode)];

            if (statusCode < HttpStatusCode.InternalServerError)
                return statusCode == HttpStatusCode.RequestTimeout;

            return false;
        }

        private static async Task<HttpResponseMessage> Send(HttpClient client, WebHookReporterOptions options, string payload, ILogger logger, CancellationToken cancellation)
        {

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(client.BaseAddress + options.Url),
                Content = new StringContent(payload)
            };

            if (options.Authorization != null)
                request.Headers.Authorization = new AuthenticationHeaderValue(options.Authorization.Scheme, options.Authorization.Parameter);

            foreach (var header in options.Headers)
            {
                request.Headers.Add(header.Name, header.Value);
            }

            var response = await client.SendAsync(request, cancellation);

            logger.LogDebug($"Report send  to URL: {client.BaseAddress + options.Url} with received status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(response.ReasonPhrase) { Data = { [nameof(HttpStatusCode)] = response.StatusCode } };

            return response;
        }
    }
}