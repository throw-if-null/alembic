using System;

namespace Alembic.Docker.Contracts
{
    public class Container
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Image { get; set; }

        public string Status { get; set; }

        public ContainerConfig Config { get; set; }

        public ContainerState State { get; set; }
    }
}