namespace Alembic.Docker.Reporting
{
    public class EventToReport
    {
        public string ContainerId { get; set; }

        public string Image { get; set; }

        public string Service { get; set; }

        public bool ActionFailed { get; set; }

    }

    public class SlackMessage
    {
        public string Text { get; set; }

        public Block[] Blocks { get; set; }
    }

    public class Block
    {
        public string Type { get; set; }

        public BlockText Text { get; set; }
    }

    public class BlockText
    {
        public string Type { get; set; }

        public string Text { get; set; }
    }
}