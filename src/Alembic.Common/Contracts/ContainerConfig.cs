using System;
using System.Collections.Generic;

namespace Alembic.Common.Contracts
{
    public class ContainerConfig
    {
        public DateTime Created { get; set; }

        public string HostName { get; set; }

        public string Image { get; set; }

        public Dictionary<string, string> Labels { get; set; }
    }
}