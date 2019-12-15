using System;

namespace Alembic.Docker.Contracts
{
    public class ContainerState
    {
        public string Error { get; set; }

        public string ExitCode { get; set; }

        public DateTime FinishedAt { get; set; }

        public bool OOMKilled { get; set; }

        public bool Dead { get; set; }

        public bool Paused { get; set; }

        public int Pid { get; set; }

        public bool Restarting { get; set; }

        public bool Running { get; set; }

        public DateTime StartedAt { get; set; }
        public string Status { get; set; }
    }
}