using System;

namespace Alembic.Common.Contracts
{
    public class ContainerHealth
    {
        public string Status { get; set; }

        public int FailingStreak { get; set; }

        public HealthLog[] Log { get; set; } = Array.Empty<HealthLog>();
    }
}