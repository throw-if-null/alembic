using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Docker.Services
{
    public interface IReporter
    {
        Task Send<T>(T payload, CancellationToken cancellation);
    }
}