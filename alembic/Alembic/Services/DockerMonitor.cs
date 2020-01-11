using Alembic.Docker.Contracts;
using Alembic.Docker.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
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

        public DockerMonitor(IOptions<DockerMonitorOptions> options, IDockerApi api, ILogger<DockerMonitor> logger)
        {
            _options = options.Value;
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task Run(CancellationToken cancellation)
        {
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

            await _api.MonitorHealthStatus(cancellation, _options.RestartCount, _options.KillUnhealthyContainer);
        }
    }
}