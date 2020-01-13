using System.Collections.ObjectModel;

namespace Alembic.Common.Resiliency
{
    public class RetryProviderOptions
    {
        public Collection<int> Delays { get; set; }
    }
}