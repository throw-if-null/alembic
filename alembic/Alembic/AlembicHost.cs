using Alembic.Docker.Contracts;
using Alembic.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic
{
    internal class AlembicHost : IHostedService
    {
        private readonly IDockerMonitor _monitor;
        private readonly ILogger _logger;

        public AlembicHost(IDockerMonitor monitor, ILogger<AlembicHost> logger)
        {
            _monitor = monitor;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var ping = await _monitor.Ping(cancellationToken);
            _logger.LogInformation($"Ping: {ping}");

            var containers = await _monitor.GetContainers(cancellationToken);

            foreach (var container in containers)
            {
                _logger.LogInformation($"ContainerId: {container.Id} Status: {container.Status}");

                if (!container.Status.Contains("unhealthy", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                try
                {
                    var ctn = await _monitor.InspectContainer(container.Id, cancellationToken);

                    var serviceName = ctn.ExtractServiceLabelValue();
                    var containerNumber = ctn.ExtractContainerNumberLabelValue();
                    var autoHeal = ctn.ExtractAutoHealLabelValue();

                    _logger.LogInformation($"Service: {serviceName} Container Number: {containerNumber} is unhealthy and it will be restarted: {autoHeal}");

                    if (!autoHeal)
                        continue;

                    var status = await _monitor.RestartContainer(ctn.Id, cancellationToken);

                    if (status == HttpStatusCode.NoContent)
                        _logger.LogInformation($"Container: {ctn.Id} restarted successfully.");
                    else
                        _logger.LogWarning($"Container: {ctn.Id} could not be restarted. Received status: {status}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to inspect container: {container.Id}");
                }
            }

            await _monitor.MonitorHealthStatus(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Alembic stopped.");

            return Task.CompletedTask;
        }
    }
}
