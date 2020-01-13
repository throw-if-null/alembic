using Newtonsoft.Json;

namespace Alembic.Docker.Contracts
{
    public class ContainerHealth
    {
        public string Status { get; set; }

        public int FailingStreak { get; set; }

        [JsonProperty("Log")]
        public HealthLog[] Logs { get; set; }
    }
}