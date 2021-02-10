using Alembic.Common.Contracts;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Common.Services
{
    public interface IDockerApi
    {
        Task<string> Ping(CancellationToken cancellation);

        Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation);

        Task<Container> InspectContainer(string id, CancellationToken cancellation);

        Task<HttpStatusCode> RestartContainer(string id, CancellationToken cancellation);

        Task<HttpStatusCode> KillContainer(string id, CancellationToken cancellation);

        Task MonitorHealthStatus(CancellationToken cancellation, int restartCount = 3, bool killUnhealthyContainer = true);
    }
}