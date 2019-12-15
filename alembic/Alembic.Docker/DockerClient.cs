using Alembic.Docker.Streaming;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Docker
{
    public sealed class DockerClient
    {
        private const string UserAgent = "Alembic";

        private static readonly TimeSpan InfiniteTimeout = TimeSpan.FromMilliseconds(Timeout.Infinite);

        public delegate void ApiResponseErrorHandlingDelegate(HttpStatusCode statusCode, string responseBody);

        public readonly IEnumerable<ApiResponseErrorHandlingDelegate> NoErrorHandlers = Enumerable.Empty<ApiResponseErrorHandlingDelegate>();

        private readonly HttpClient _client;
        private readonly Uri _endpointBaseUri;
        private readonly Version _requestedApiVersion;
        private readonly DockerClientConfiguration _configuration;
        private readonly TimeSpan _defaultTimeout;

        public DockerClient(DockerClientConfiguration configuration, Version requestedApiVersion)
        {
            _configuration = configuration;
            _requestedApiVersion = requestedApiVersion;

            ManagedHandler handler;
            var uri = _configuration.EndpointBaseUri;
            switch (uri.Scheme.ToLowerInvariant())
            {
                case "npipe":
                    if (_configuration.Credentials.IsTlsCredentials())
                    {
                        throw new Exception("TLS not supported over npipe");
                    }

                    var segments = uri.Segments;
                    if (segments.Length != 3 || !segments[1].Equals("pipe/", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArgumentException($"{_configuration.EndpointBaseUri} is not a valid npipe URI");
                    }

                    var serverName = uri.Host;
                    if (string.Equals(serverName, "localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        // npipe schemes dont work with npipe://localhost/... and need npipe://./... so fix that for a client here.
                        serverName = ".";
                    }

                    var pipeName = uri.Segments[2];

                    uri = new UriBuilder("http", pipeName).Uri;
                    handler = new ManagedHandler(async (host, port, cancellationToken) =>
                    {
                        int timeout = (int)this._configuration.NamedPipeConnectTimeout.TotalMilliseconds;
                        var stream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        var dockerStream = new DockerPipeStream(stream);

                        await stream.ConnectAsync(timeout, cancellationToken);

                        return dockerStream;
                    });

                    break;

                case "unix":
                    var pipeString = uri.LocalPath;
                    handler = new ManagedHandler(async (string host, int port, CancellationToken cancellationToken) =>
                    {
                        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        await sock.ConnectAsync(new UnixDomainSocketEndPoint(pipeString));
                        return sock;
                    });
                    uri = new UriBuilder("http", uri.Segments.Last()).Uri;
                    break;

                default:
                    throw new Exception($"Unknown URL scheme {configuration.EndpointBaseUri.Scheme}");
            }

            _endpointBaseUri = uri;

            _client = new HttpClient(_configuration.Credentials.GetHandler(handler), true);
            _defaultTimeout = _configuration.DefaultTimeout;
            _client.Timeout = InfiniteTimeout;
        }

        public async Task<(HttpStatusCode, string)> MakeRequestAsync(
            IEnumerable<ApiResponseErrorHandlingDelegate> errorHandlers,
            HttpMethod method,
            string path,
            string queryString,
            IDictionary<string, string> headers,
            TimeSpan timeout,
            CancellationToken token)
        {
            var response = await PrivateMakeRequestAsync(timeout, HttpCompletionOption.ResponseContentRead, method, path, queryString, headers, token).ConfigureAwait(false);

            using (response)
            {
                await HandleIfErrorResponseAsync(response.StatusCode, response, errorHandlers);

                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

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
            var response = await PrivateMakeRequestAsync(timeout, HttpCompletionOption.ResponseHeadersRead, method, path, queryString, headers, token).ConfigureAwait(false);

            await HandleIfErrorResponseAsync(response.StatusCode, response, errorHandlers);

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
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
                using (var timeoutTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutTokenSource.CancelAfter(timeout);

                    // We must await here because we need to dispose of the CTS only after the work has been completed.
                    return await PrivateMakeRequestAsync(InfiniteTimeout, completionOption, method, path, queryString, headers, timeoutTokenSource.Token).ConfigureAwait(false);
                }
            }

            var request = PrepareRequest(method, path, queryString, headers);

            return await _client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        }

        internal HttpRequestMessage PrepareRequest(HttpMethod method, string path, string queryString, IDictionary<string, string> headers)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var url = $"{_endpointBaseUri}v{_requestedApiVersion}/{path}";

            if (!string.IsNullOrWhiteSpace(queryString))
                url = $"{url}?{queryString}";

            var request = new HttpRequestMessage(method, new Uri(url));

            request.Version = new Version(1, 40);

            request.Headers.Add("User-Agent", UserAgent);

            if(headers == null)
                return request;

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            return request;
        }

        private async Task HandleIfErrorResponseAsync(HttpStatusCode statusCode, HttpResponseMessage response, IEnumerable<ApiResponseErrorHandlingDelegate> handlers)
        {
            bool isErrorResponse = statusCode < HttpStatusCode.OK || statusCode >= HttpStatusCode.BadRequest;

            string responseBody = null;

            if (isErrorResponse)
            {
                // If it is not an error response, we do not read the response body because the caller may wish to consume it.
                // If it is an error response, we do because there is nothing else going to be done with it anyway and
                // we want to report the response body in the error message as it contains potentially useful info.
                responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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
            {
                throw new DockerApiException(statusCode, responseBody);
            }
        }

        public void Dispose()
        {
            _configuration.Dispose();
        }
    }
}