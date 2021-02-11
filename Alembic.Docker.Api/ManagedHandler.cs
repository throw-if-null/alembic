using Alembic.Docker.Streaming;
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Docker
{
    public class ManagedHandler : HttpMessageHandler
    {
        public delegate Task<Stream> StreamOpener(string host, int port, CancellationToken cancellationToken);
        public delegate Task<Socket> SocketOpener(string host, int port, CancellationToken cancellationToken);

        private readonly StreamOpener _streamOpener;
        private readonly SocketOpener _socketOpener;

        public ManagedHandler()
        {
            _socketOpener = TCPSocketOpenerAsync;
        }

        public ManagedHandler(StreamOpener opener)
        {
            _streamOpener = opener ?? throw new ArgumentNullException(nameof(opener));
        }

        public ManagedHandler(SocketOpener opener)
        {
            _socketOpener = opener ?? throw new ArgumentNullException(nameof(opener));
        }

        public int MaxAutomaticRedirects { get; set; } = 20;

        public RedirectMode RedirectMode { get; set; } = RedirectMode.NoDowngrade;

        public X509CertificateCollection ClientCertificates { get; set; }

        public RemoteCertificateValidationCallback ServerCertificateValidationCallback { get; set; }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            HttpResponseMessage response = null;
            int redirectCount = 0;
            bool retry;

            do
            {
                retry = false;
                response = await ProcessRequestAsync(request, cancellationToken);
                if (redirectCount < MaxAutomaticRedirects && IsAllowedRedirectResponse(request, response))
                {
                    redirectCount++;
                    retry = true;
                }

            } while (retry);

            return response;
        }

        private bool IsAllowedRedirectResponse(HttpRequestMessage request, HttpResponseMessage response)
        {
            // Are redirects enabled?
            if (RedirectMode == RedirectMode.None)
            {
                return false;
            }

            // Status codes 301 and 302
            if (response.StatusCode != HttpStatusCode.Redirect && response.StatusCode != HttpStatusCode.Moved)
            {
                return false;
            }

            Uri location = response.Headers.Location;

            if (location == null)
            {
                return false;
            }

            if (!location.IsAbsoluteUri)
            {
                request.RequestUri = location;
                request.SetPathAndQueryProperty(null);
                request.SetAddressLineProperty(null);
                request.Headers.Authorization = null;
                return true;
            }

            // Check if redirect from https to http is allowed
            if (request.IsHttps() && string.Equals("http", location.Scheme, StringComparison.OrdinalIgnoreCase)
                && RedirectMode == RedirectMode.NoDowngrade)
            {
                return false;
            }

            // Reset fields calculated from the URI.
            request.RequestUri = location;
            request.SetSchemeProperty(null);
            request.Headers.Host = null;
            request.Headers.Authorization = null;
            request.SetHostProperty(null);
            request.SetConnectionHostProperty(null);
            request.SetPortProperty(null);
            request.SetConnectionPortProperty(null);
            request.SetPathAndQueryProperty(null);
            request.SetAddressLineProperty(null);

            return true;
        }

        private async Task<HttpResponseMessage> ProcessRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessUrl(request);
            ProcessHostHeader(request);
            request.Headers.ConnectionClose = true; // TODO: Connection re-use is not supported.

            string pathAndQuery = request.GetPathAndQueryProperty();
            string addressLine = request.GetAddressLineProperty();

            if (string.IsNullOrEmpty(addressLine))
                request.SetAddressLineProperty(pathAndQuery);

            Socket socket;
            Stream transport;

            try
            {
                if (_socketOpener != null)
                {
                    socket = await _socketOpener(request.GetConnectionHostProperty(), request.GetConnectionPortProperty().Value, cancellationToken);
                    transport = new NetworkStream(socket, true);
                }
                else
                {
                    socket = null;
                    transport = await _streamOpener(request.GetConnectionHostProperty(), request.GetConnectionPortProperty().Value, cancellationToken);
                }
            }
            catch (SocketException sox)
            {
                throw new HttpRequestException("Connection failed", sox);
            }

            if (request.IsHttps())
            {
                var sslStream = new SslStream(transport, false, ServerCertificateValidationCallback);
                await sslStream.AuthenticateAsClientAsync(request.GetHostProperty(), ClientCertificates, SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls, false);
                transport = sslStream;
            }

            var bufferedReadStream = new BufferedReadStream(transport, socket);
            var connection = new HttpConnection(bufferedReadStream);

            return await connection.SendAsync(request, cancellationToken);
        }

        // Data comes from either the request.RequestUri or from the request.Properties
        private void ProcessUrl(HttpRequestMessage request)
        {
            string scheme = request.GetSchemeProperty();
            if (string.IsNullOrWhiteSpace(scheme))
            {
                if (!request.RequestUri.IsAbsoluteUri)
                    throw new InvalidOperationException("Missing URL Scheme");

                scheme = request.RequestUri.Scheme;
                request.SetSchemeProperty(scheme);
            }

            if (!(request.IsHttp() || request.IsHttps()))
                throw new InvalidOperationException("Only HTTP or HTTPS are supported, not: " + request.RequestUri.Scheme);

            string host = request.GetHostProperty();
            if (string.IsNullOrWhiteSpace(host))
            {
                if (!request.RequestUri.IsAbsoluteUri)
                    throw new InvalidOperationException("Missing URL Scheme");

                host = request.RequestUri.DnsSafeHost;
                request.SetHostProperty(host);
            }

            string connectionHost = request.GetConnectionHostProperty();
            if (string.IsNullOrWhiteSpace(connectionHost))
                request.SetConnectionHostProperty(host);

            int? port = request.GetPortProperty();
            if (!port.HasValue)
            {
                if (!request.RequestUri.IsAbsoluteUri)
                    throw new InvalidOperationException("Missing URL Scheme");

                port = request.RequestUri.Port;
                request.SetPortProperty(port);
            }

            int? connectionPort = request.GetConnectionPortProperty();
            if (!connectionPort.HasValue)
                request.SetConnectionPortProperty(port);

            string pathAndQuery = request.GetPathAndQueryProperty();
            if (string.IsNullOrWhiteSpace(pathAndQuery))
            {
                if (request.RequestUri.IsAbsoluteUri)
                    pathAndQuery = request.RequestUri.PathAndQuery;
                else
                    pathAndQuery = Uri.EscapeUriString(request.RequestUri.ToString());

                request.SetPathAndQueryProperty(pathAndQuery);
            }
        }

        private void ProcessHostHeader(HttpRequestMessage request)
        {
            if (string.IsNullOrWhiteSpace(request.Headers.Host))
            {
                string host = request.GetHostProperty();
                int port = request.GetPortProperty().Value;
                if (host.Contains(':'))
                {
                    // IPv6
                    host = '[' + host + ']';
                }

                request.Headers.Host = host + ":" + port.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static async Task<Socket> TCPSocketOpenerAsync(string host, int port, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            if (addresses.Length == 0)
            {
                throw new Exception($"could not resolve address for {host}");
            }

            Socket connectedSocket = null;
            Exception lastException = null;
            foreach (var address in addresses)
            {
                var s = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await s.ConnectAsync(address, port);

                    connectedSocket = s;
                    break;
                }
                catch (Exception e)
                {
                    s.Dispose();
                    lastException = e;
                }
            }

            if (connectedSocket == null)
            {
                throw lastException;
            }

            return connectedSocket;
        }
    }
}