using System.Collections.ObjectModel;

namespace Alembic.Docker.Infrastructure
{
    public class RetryProviderOptions
    {
        public Collection<int> Delays { get; set; }
    }
}