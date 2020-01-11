using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Reporting
{
    public interface IReporter
    {
        Task Send<T>(T payload, CancellationToken cancellation);
    }
}