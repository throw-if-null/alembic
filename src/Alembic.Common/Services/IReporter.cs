using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Common.Services
{
    public interface IReporter
    {
        Task Send<T>(T payload, CancellationToken cancellation);
    }
}