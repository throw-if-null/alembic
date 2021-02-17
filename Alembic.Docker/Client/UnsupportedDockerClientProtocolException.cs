using System;

namespace Alembic.Docker.Client
{
    public class UnsupportedDockerClientProtocolException : Exception
    {
        public UnsupportedDockerClientProtocolException(string schema)
            : base($"Schema: {schema} is not supported.")
        {
        }
    }
}
