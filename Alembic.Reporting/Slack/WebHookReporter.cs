using Alembic.Common.Resiliency;
using Alembic.Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Reporting.Slack
{
    public class WebHookReporter : IReporter
    {
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
            _client.BaseAddress = new Uri("https://hooks.slack.com/");
            _retryProvider = retryProvider;
            _logger = logger;
        }

        public async Task Send<T>(T payload, CancellationToken cancellation)
        {
            using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancellation);

            try
            {
                await
                    _retryProvider.RetryOn<HttpRequestException, HttpResponseMessage>(
                        CheckError,
                        TransientHttpStatusCodePredicate,
                        () => Send(_client, _options, JsonConvert.SerializeObject(payload, Formatting.Indented), _logger, linkedSource.Token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report sending failed");
            }
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