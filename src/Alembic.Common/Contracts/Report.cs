namespace Alembic.Common.Contracts
{
    public class Report
    {
        public string Message { get; set; }

        public ContainerOperation Operation { get; set; }

        public Container Container { get; set; }

        private Report()
        {
        }

        public static Report CreateRestart(string message, Container container)
        {
            return Create(ContainerOperation.Restart, message, container);
        }

        public static Report CreateKill(string message, Container container)
        {
            return Create(ContainerOperation.Kill, message, container);
        }

        private static Report Create(ContainerOperation operation, string message, Container container)
        {
            return new Report
            {
                Operation = operation,
                Message = message,
                Container = container
            };
        }
    }
}