using System.Collections.Generic;

namespace Alembic.Common.Contracts
{
    public class ContainerInfo
    {
        public string Id { get; set; }

        public string Status { get; set; }

        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    }
}