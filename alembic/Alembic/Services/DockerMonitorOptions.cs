namespace Alembic.Services
{
    public class DockerMonitorOptions
    {
        public int RestartCount { get; set; } = 3;

        public bool KillUnhealthyContainer { get; set; } = true;
    }
}