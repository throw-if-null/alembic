using System.Threading;
using System.Threading.Tasks;
using Alembic.Common.Contracts;

namespace Alembic.Common.Services
{
    public interface IReporter
    {
        Task Send(Report report, CancellationToken cancellation);
    }
}