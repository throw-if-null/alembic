using Alembic.Docker.Api.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using static Alembic.Docker.Api.DockerClient;

namespace Alembic.Docker.Api
{
    public interface IDockerClient
    {
        Task<(HttpStatusCode responseStatus, string responseBody)> MakeRequestAsync(IEnumerable<ApiResponseErrorHandlingDelegate> errorHandlers, HttpMethod method, string path, string queryString, IDictionary<string, string> headers, TimeSpan timeout, CancellationToken token);

        Task<Stream> MakeRequestForStreamAsync(IEnumerable<ApiResponseErrorHandlingDelegate> errorHandlers, HttpMethod method, string path, string queryString, IDictionary<string, string> headers, TimeSpan timeout, CancellationToken token);
    }

    public sealed class DockerClient : IDockerClient
    {
        private const string UserAgent = "Alembic";

        private static readonly TimeSpan InfiniteTimeout = TimeSpan.FromMilliseconds(Timeout.Infinite);
        private static readonly Version ApiVersion = new Version("1.40");

        public delegate void ApiResponseErrorHandlingDelegate(HttpStatusCode statusCode, string responseBody);

        private readonly IDockerClientFactory _factory;

        public DockerClient(IDockerClientFactory factory)
        {
            _factory = factory;
        }

        public async Task<(HttpStatusCode responseStatus, string responseBody)> MakeRequestAsync(
            IEnumerable<ApiResponseErrorHandlingDelegate> errorHandlers,
            HttpMethod method,
            string path,
            string queryString,
            IDictionary<string, string> headers,
            TimeSpan timeout,
            CancellationToken token)
        {
            var response = await PrivateMakeRequestAsync(timeout, HttpCompletionOption.ResponseContentRead, method, path, queryString, headers, token);

            using (response)
            {
                await HandleIfErrorResponseAsync(response.StatusCode, response, errorHandlers);

                var responseBody = await response.Content.ReadAsStringAsync();

                return (response.StatusCode, responseBody);
            }
        }

        public async Task<Stream> MakeRequestForStreamAsync(
            IEnumerable<ApiResponseErrorHandlingDelegate> errorHandlers,
            HttpMethod method,
            string path,
            string queryString,
            IDictionary<string, string> headers,
            TimeSpan timeout,
            CancellationToken token)
        {
            var response = await PrivateMakeRequestAsync(timeout, HttpCompletionOption.ResponseHeadersRead, method, path, queryString, headers, token);

            await HandleIfErrorResponseAsync(response.StatusCode, response, errorHandlers);

            return await response.Content.ReadAsStreamAsync();
        }

        private async Task<HttpResponseMessage> PrivateMakeRequestAsync(
            TimeSpan timeout,
            HttpCompletionOption completionOption,
            HttpMethod method,
            string path,
            string queryString,
            IDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            // If there is a timeout, we turn it into a cancellation token. At the same time, we need to link to the caller's
            // cancellation token. To avoid leaking objects, we must then also dispose of the CancellationTokenSource. To keep
            // code flow simple, we treat it as re-entering the same method with a different CancellationToken and no timeout.
            if (timeout != InfiniteTimeout)
            {
                using var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeoutTokenSource.CancelAfter(timeout);

                // We must await here because we need to dispose of the CTS only after the work has been completed.
                return
                    await
                        PrivateMakeRequestAsync(InfiniteTimeout, completionOption, method, path, queryString, headers, timeoutTokenSource.Token);
            }

            var request = PrepareRequest(method, _factory.GetOrCreate().BaseAddress, path, queryString, headers);

            var response = await _factory.GetOrCreate().SendAsync(request, completionOption, cancellationToken);

            return response;
        }

        private static HttpRequestMessage PrepareRequest(HttpMethod method, Uri baseUri, string path, string queryString, IDictionary<string, string> headers)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var url = $"{baseUri}v{ApiVersion}/{path}";

            if (!string.IsNullOrWhiteSpace(queryString))
                url = $"{url}?{queryString}";

            var request = new HttpRequestMessage(method, new Uri(url));

            request.Version = new Version(1, 40);

            request.Headers.Add("User-Agent", UserAgent);

            if (headers == null)
                return request;

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            return request;
        }

        private static async Task HandleIfErrorResponseAsync(HttpStatusCode statusCode, HttpResponseMessage response, IEnumerable<ApiResponseErrorHandlingDelegate> handlers)
        {
            bool isErrorResponse = statusCode < HttpStatusCode.OK || statusCode >= HttpStatusCode.BadRequest;

            string responseBody = string.Empty;

            if (isErrorResponse)
            {
                // If it is not an error response, we do not read the response body because the caller may wish to consume it.
                // If it is an error response, we do because there is nothing else going to be done with it anyway and
                // we want to report the response body in the error message as it contains potentially useful info.
                responseBody = await response.Content.ReadAsStringAsync();
            }

            // If no customer handlers just default the response.
            if (handlers != null)
            {
                foreach (var handler in handlers)
                {
                    handler(statusCode, responseBody);
                }
            }

            // No custom handler was fired. Default the response for generic success/failures.
            if (isErrorResponse)
                throw new DockerApiException(statusCode, responseBody);
        }
    }
}