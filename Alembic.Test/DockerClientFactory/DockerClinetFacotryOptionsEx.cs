using Alembic.Docker.Client;
using Microsoft.Extensions.Options;
using System;

namespace Alembic.Test
{
    public class DockerClinetFacotryOptionsEx : IOptionsMonitor<DockerClientFactoryOptions>
    {
        public DockerClientFactoryOptions CurrentValue { get; }

        public DockerClinetFacotryOptionsEx(string baseUri)
        {
            CurrentValue = new DockerClientFactoryOptions { BaseUri = baseUri };
        }

        public DockerClientFactoryOptions Get(string name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<DockerClientFactoryOptions, string> listener)
        {
            return null;
        }
    }
}
