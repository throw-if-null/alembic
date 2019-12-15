using System;
using System.Net.Http;

namespace Alembic.Docker
{
    public abstract class Credentials : IDisposable
    {
        public abstract bool IsTlsCredentials();

        public abstract HttpMessageHandler GetHandler(HttpMessageHandler innerHandler);

        public virtual void Dispose()
        {
        }
    }

    public class AnonymousCredentials : Credentials
    {
        public override bool IsTlsCredentials()
        {
            return false;
        }

        public override void Dispose()
        {
        }

        public override HttpMessageHandler GetHandler(HttpMessageHandler innerHandler)
        {
            return innerHandler;
        }
    }
}