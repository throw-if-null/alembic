using Alembic.Common.Contracts;
using Alembic.Common.Services;
using Alembic.Docker;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Test
{
    public class DockerApiEx : IDockerApi
    {
        private readonly DockerApi _api;

        public DockerApiEx(DockerApi api)
        {
            _api = api;
        }

        public Task<string> Ping(CancellationToken cancellation)
        {
            return _api.Ping(cancellation);
        }

        public Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation)
        {
            return _api.GetContainers(cancellation);
        }

        public Task<Container> InspectContainer(string id, CancellationToken cancellation)
        {
            return _api.InspectContainer(id, cancellation);
        }

        public Task<HttpStatusCode> KillContainer(string id, CancellationToken cancellation)
        {
            return _api.KillContainer(id, cancellation);
        }

        public Task MonitorHealthStatus(CancellationToken cancellation, int restartCount = 3, bool killUnhealthyContainer = true, Action<UnhealthyStatusActionReport> onUnheathyStatusReceived = null)
        {
            return _api.MonitorHealthStatus(cancellation, restartCount, killUnhealthyContainer, onUnheathyStatusReceived);
        }

        public Task<HttpStatusCode> RestartContainer(string id, CancellationToken cancellation)
        {
            return _api.RestartContainer(id, cancellation);
        }

        internal void SetContainerRetryCount(string containerId, int count)
        {
            _api._containerRetries[containerId] = count;
        }

        internal bool HasContainerBeenRestarted(string id)
        {
            return _api._containerRetries.TryGetValue(id, out _);
        }
    }
}
