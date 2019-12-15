using System.IO;

namespace Alembic.Docker.Streaming
{
    public abstract class WriteClosableStream : Stream
    {
        public abstract bool CanCloseWrite { get; }

        public abstract void CloseWrite();
    }
}