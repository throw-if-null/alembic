using Alembic.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
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
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Alembic is running.");

            return _monitor.Run(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Alembic stopped.");

            return Task.CompletedTask;
        }
    }
}
