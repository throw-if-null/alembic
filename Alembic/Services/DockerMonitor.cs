using Alembic.Common.Contracts;
using Alembic.Common.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Services
{
    public interface IDockerMonitor
    {
        Task Run(CancellationToken cancellation);
    }

    public class DockerMonitor : IDockerMonitor
    {
        private readonly DockerMonitorOptions _options;
        private readonly IDockerApi _api;
        private readonly ILogger _logger;

        public DockerMonitor(IOptionsMonitor<DockerMonitorOptions> options, IDockerApi api, ILogger<DockerMonitor> logger)
        {
            _options = options.CurrentValue;
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run(CancellationToken cancellation)
        {
            await Ping(cancellation);

            var containers = await _api.GetContainers(cancellation);

            foreach (var container in containers)
            {
                _logger.LogInformation($"ContainerId: {container.Id} Status: {container.Status}");

                if (!container.Status.Contains("unhealthy", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                try
                {
                    var ctn = await _api.InspectContainer(container.Id, cancellation);

                    var serviceName = ctn.ExtractServiceLabelValue();
                    var containerNumber = ctn.ExtractContainerNumberLabelValue();
                    var autoHeal = ctn.ExtractAutoHealLabelValue();

                    _logger.LogInformation($"Service: {serviceName} Container Number: {containerNumber} is unhealthy and it will be restarted: {autoHeal}");

                    if (!autoHeal)
                        continue;

                    var status = await _api.RestartContainer(ctn.Id, cancellation);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to inspect container: {container.Id}");
                }
            }

            await _api.MonitorHealthStatus(cancellation, _options.RestartCount, _options.KillUnhealthyContainer);
        }

        private async Task Ping(CancellationToken cancellation)
        {
            var ping = await _api.Ping(cancellation);

            _logger.LogDebug($"Docker enging ping: {ping}");
        }
    }
}