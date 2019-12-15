using Alembic.Docker;
using Alembic.Services;
using System;
using System.Collections.Generic;
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

            DockerClient client = new DockerClientConfiguration().CreateClient();
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
    }
}