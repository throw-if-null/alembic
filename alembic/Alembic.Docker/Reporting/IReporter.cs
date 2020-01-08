using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Docker.Reporting
{
    public interface IReporter
    {
        Task Send<T>(T payload, CancellationToken cancellation);
    }
}