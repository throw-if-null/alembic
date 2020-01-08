using System.Collections.ObjectModel;

namespace Alembic.Docker.Reporting
{
    public class WebHookReporterOptions
    {
        public string Url { get; set; }

        public int TimeoutInMs { get; set; }

        public Authorization Authorization { get; set; }

        public Collection<RequestHeader> Headers { get; set; } = new Collection<RequestHeader>();
    }

    public class Authorization
    {
        public string Scheme { get; set; }

        public string Parameter { get; set; }
    }

    public class RequestHeader
    {
        public string Name { get; set; }

        public string Value { get; set; }
    }
}
