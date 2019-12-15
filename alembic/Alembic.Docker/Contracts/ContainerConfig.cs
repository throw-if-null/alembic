using System;

namespace Alembic.Docker.Contracts
{
    public class ContainerConfig
    {
        public DateTime Created { get; set; }

        public string HostName { get; set; }

        public string Image { get; set; }

        public ContainerLabels Labels { get; set; }
    }
}