using System.IO;

namespace Alembic.Docker.Api.Streaming
{
    public abstract class WriteClosableStream : Stream
    {
        public abstract bool CanCloseWrite { get; }

        public abstract void CloseWrite();
    }
}