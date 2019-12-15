using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Alembic.Docker
{
    public class DockerClientConfiguration : IDisposable
    {
        private const string Version = "1.40";

        public Uri EndpointBaseUri { get; internal set; }

        public Credentials Credentials { get; internal set; }

        public TimeSpan DefaultTimeout { get; internal set; } = TimeSpan.FromSeconds(100);

        public TimeSpan NamedPipeConnectTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

        public DockerClientConfiguration() : this(DockerApiUri())
        {
        }

        public DockerClientConfiguration(Uri endpoint, Credentials credentials = null, TimeSpan defaultTimeout = default)
        {
            _ = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Credentials = credentials ?? new AnonymousCredentials();

            EndpointBaseUri = endpoint;

            if (defaultTimeout != TimeSpan.Zero)
            {
                if (defaultTimeout < Timeout.InfiniteTimeSpan)
                    throw new ArgumentException("Timeout cannot be: Timeout.Infinite", nameof(defaultTimeout));

                DefaultTimeout = defaultTimeout;
            }
        }

        public DockerClient CreateClient()
        {
            return new DockerClient(this, new Version(Version));
        }

        public void Dispose()
        {
            Credentials.Dispose();
        }

        private static Uri DockerApiUri()
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            if (isWindows)
                return new Uri("npipe://./pipe/docker_engine");

            var isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

            if (isLinux)
                return new Uri("unix:/var/run/docker.sock");

            throw new Exception("Was unable to determine what OS this is running on, does not appear to be Windows or Linux!?");
        }
    }
}