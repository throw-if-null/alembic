using Alembic.Docker;
using Alembic.Docker.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Alembic.Services
{
    public interface IDockerMonitor
    {
        Task<string> Ping(CancellationToken cancellation);

        Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation);

        Task<Container> InspectContainer(string id, CancellationToken cancellation);

        Task<HttpStatusCode> RestartContainer(string id, CancellationToken cancellation);

        Task<HttpStatusCode> KillContainer(string id, CancellationToken cancellation);

        Task MonitorHealthStatus(CancellationToken cancellation);
    }

    public class DockerMonitor : IDockerMonitor
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

        private readonly DockerApi _client;

        public DockerMonitor(DockerApi client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<string> Ping(CancellationToken cancellation)
        {
            (HttpStatusCode code, string body) = await _client.MakeRequestAsync(_client.NoErrorHandlers, HttpMethod.Get, "_ping", null, null, Timeout, cancellation);

            if (code == HttpStatusCode.OK)
                return body;

            // Log
            throw new DockerApiException(code, "Unable to ping Docker server");
        }

        public async Task<IEnumerable<ContainerInfo>> GetContainers(CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(_client.NoErrorHandlers, HttpMethod.Get, "containers/json", "all=true", null, Timeout, cancellation);

            if (status == HttpStatusCode.OK)
            {
                var containers = JsonConvert.DeserializeObject<ContainerInfo[]>(body);

                return containers;
            }

            // Log

            return Enumerable.Empty<ContainerInfo>();
        }

        public async Task<Container> InspectContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(_client.NoErrorHandlers, HttpMethod.Get, $"containers/{id}/json", null, null, Timeout, cancellation);

            if(status == HttpStatusCode.OK)
            {
                var container = JsonConvert.DeserializeObject<Container>(body);

                return container;
            }

            if(status == HttpStatusCode.NotFound)
            {
                // Log

                return null;
            }

            // Log

            return null;
        }

        public async Task<HttpStatusCode> RestartContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(_client.NoErrorHandlers, HttpMethod.Post, $"containers/{id}/restart ", null, null, Timeout, cancellation);

            // Log
            return status;
        }

        public async Task<HttpStatusCode> KillContainer(string id, CancellationToken cancellation)
        {
            (HttpStatusCode status, string body) = await _client.MakeRequestAsync(_client.NoErrorHandlers, HttpMethod.Post, $"containers/{id}/kill ", null, null, Timeout, cancellation);

            // Log
            return status;
        }

        public async Task MonitorHealthStatus(CancellationToken cancellation)
        {
            var stream = await _client.MakeRequestForStreamAsync(_client.NoErrorHandlers, HttpMethod.Get, "events", @"filters=%7B%22event%22%3A%7B%22health_status%22%3Atrue%7D%7D", null, Timeout, cancellation);

            using (cancellation.Register(() => stream.Dispose()))
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false));

                string line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    Console.WriteLine("<---------------START------------------------->");
                    Console.WriteLine(line);
                    Console.WriteLine("<---------------END------------------------->");

                    var containerHealth = JsonConvert.DeserializeObject<ContainerInfo>(line);

                    if (containerHealth.Status.Split(":")[1].Trim() == "unhealthy")
                    {
                        var restarted = await RestartContainer(containerHealth.Id, cancellation);

                        // Log add retry logic if needed.
                    }
                }
            }
        }
    }
}