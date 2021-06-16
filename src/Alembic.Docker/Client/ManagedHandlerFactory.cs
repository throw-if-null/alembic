using Alembic.Docker.Streaming;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alembic.Docker.Client
{
    public interface IManagedHandlerFactory
    {
        HttpClient GetOrCreate();
    }

    public class ManagedHandlerFactory : IManagedHandlerFactory, IDisposable
    {
        private static readonly TimeSpan InfiniteTimeout = TimeSpan.FromMilliseconds(Timeout.Infinite);

        private readonly Dictionary<Uri, HttpClient> _cache = new Dictionary<Uri, HttpClient>();
        private readonly ManagedHandlerFactoryOptions _options;
        private readonly ILogger _logger;

        private bool _disposedValue;

        public ManagedHandlerFactory(IOptionsMonitor<ManagedHandlerFactoryOptions> options, ILogger<ManagedHandlerFactory> logger)
        {
            _options = options.CurrentValue;
            _logger = logger;
        }

        public HttpClient GetOrCreate()
        {
            var (handler, uri) = ResolveHandlerAndUri(_options.BaseUri);

            if (_cache.ContainsKey(uri))
                return _cache[uri];

            _logger.LogDebug($"HttpClient created: {uri}");

            _cache[uri] = new HttpClient(handler, true)
            {
                BaseAddress = uri,
                Timeout = InfiniteTimeout
            };

            return _cache[uri];
        }

        private static (HttpMessageHandler handler, Uri uri) ResolveHandlerAndUri(string baseUrl)
        {
            ManagedHandler handler;
            var uri = new Uri(baseUrl.Equals(".") ? DockerApiUri() : baseUrl);

            switch (uri.Scheme.ToLowerInvariant())
            {
                case "npipe":
                    var segments = uri.Segments;

                    if (segments.Length != 3 || !segments[1].Equals("pipe/", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException($"{baseUrl} is not a valid npipe URI");

                    var serverName = uri.Host;

                    // npipe schemes dont work with npipe://localhost/... and need npipe://./... so fix that for a client here.
                    serverName = string.Equals(serverName, "localhost", StringComparison.OrdinalIgnoreCase) ? "." : serverName;

                    var pipeName = uri.Segments[2];

                    uri = new UriBuilder("http", pipeName).Uri;
                    handler = new ManagedHandler(async (host, port, cancellationToken) =>
                    {
                        var stream = new NamedPipeClientStream(serverName, pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                        var dockerStream = new DockerPipeStream(stream);

                        await stream.ConnectAsync(100, cancellationToken);

                        return dockerStream;
                    });

                    break;

                case "unix":
                    handler = new ManagedHandler(async (string host, int port, CancellationToken cancellationToken) =>
                    {
                        var sock = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        await sock.ConnectAsync(new UnixDomainSocketEndPoint(uri.LocalPath), cancellationToken: cancellationToken);

                        return sock;
                    });

                    uri = new UriBuilder("http", uri.Segments.Last()).Uri;

                    break;

                default:
                    throw new UnsupportedProtocolException(uri.Scheme);
            }

            return (handler, uri);
        }

        private static string DockerApiUri()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
                return @"npipe://./pipe/docker_engine";

            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (isLinux)
                return @"unix:/var/run/docker.sock";

            throw new Exception("Was unable to determine what OS this is running on, does not appear to be Windows or Linux!?");
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach(var item in _cache)
                    {
                        item.Value.Dispose();
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}