namespace Alembic.Common.Contracts
{
    public sealed class UnhealthyStatusActionReport
    {
        public int RestartCount { get; set; }

        public bool Restarted { get; set; }

        public bool Killed { get; set; }

        public UnhealthyStatusActionReport(int restartCount, bool restarted, bool killed)
        {
            RestartCount = restartCount;
            Restarted = restarted;
            Killed = killed;
        }
    }
}
