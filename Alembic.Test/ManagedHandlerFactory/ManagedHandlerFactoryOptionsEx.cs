using Alembic.Docker.Client;
using Microsoft.Extensions.Options;
using System;

namespace Alembic.Test
{
    public class ManagedHandlerFactoryOptionsEx : IOptionsMonitor<ManagedHandlerFactoryOptions>
    {
        public ManagedHandlerFactoryOptions CurrentValue { get; }

        public ManagedHandlerFactoryOptionsEx(string baseUri)
        {
            CurrentValue = new ManagedHandlerFactoryOptions { BaseUri = baseUri };
        }

        public ManagedHandlerFactoryOptions Get(string name)
        {
            return CurrentValue;
        }

        public IDisposable OnChange(Action<ManagedHandlerFactoryOptions, string> listener)
        {
            return null;
        }
    }
}
