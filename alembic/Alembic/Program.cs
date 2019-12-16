using Alembic.Docker;
using Alembic.Docker.Client;
using Alembic.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic
{
    class Program
    {
        private static readonly IReadOnlyCollection<string> Container_Events = new[] {
            "attach",
            "commit",
            "copy",
            "create",
            "destroy",
            "detach",
            "die",
            "exec_create",
            "exec_detach",
            "exec_start",
            "exec_die",
            "export",
            "health_status",
            "kill",
            "oom",
            "pause",
            "rename",
            "resize",
            "restart",
            "start",
            "stop",
            "top",
            "unpause",
            "update"
        };

        static async Task Main(string[] args)
        {
            var cancellationToken = CancellationToken.None;

            var services = new ServiceCollection();

            services.AddSingleton<DockerApi>();
            services.AddTransient<IDockerMonitor, DockerMonitor>();
            services.AddSingleton<IDockerClientFactory, DockerClientFactory>();

            var provider = services.BuildServiceProvider();

            DockerApi client = new DockerApi(
                new DockerClientConfiguration(),
                new DockerClientFactory(Options.Create(new DockerClientFactoryOptions { BaseUri = DockerApiUri() })));

            var monitor = new DockerMonitor(client);

            var ping = await monitor.Ping(cancellationToken);
            Console.WriteLine($"Ping: {ping}");

            var containers = await monitor.GetContainers(cancellationToken);

            foreach (var container in containers)
            {
                Console.WriteLine($"ContainerId: {container.Id} Status: {container.Status}");

                try
                {
                    _ = await monitor.InspectContainer(container.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            await monitor.MonitorHealthStatus(cancellationToken);
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