using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Docker.Streaming
{
    internal static class StreamUtil
    {
        internal static async Task MonitorStreamAsync(Task<Stream> streamTask, CancellationToken cancel, IProgress<string> progress)
        {
            using (var stream = await streamTask)
            {
                // ReadLineAsync must be cancelled by closing the whole stream.
                using (cancel.Register(() => stream.Dispose()))
                {
                    using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            progress.Report(line);
                        }
                    }
                }
            }
        }
    }
}