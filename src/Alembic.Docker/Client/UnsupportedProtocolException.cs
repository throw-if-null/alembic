using System;

namespace Alembic.Docker.Client
{
    public class UnsupportedProtocolException : Exception
    {
        public UnsupportedProtocolException(string schema)
            : base($"Schema: {schema} is not supported.")
        {
        }
    }
}
