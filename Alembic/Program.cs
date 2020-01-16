using Alembic.Common.Resiliency;
using Alembic.Docker.Api;
using Alembic.Docker.Api.Client;
using Alembic.Docker.Services;
using Alembic.Reporting.Slack;
using Alembic.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Alembic
{
    internal class Program
    {
        internal static Task Main(string[] args)
        {
            return CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DockerClientFactoryOptions>(context.Configuration.GetSection("DockerClientFactoryOptions"));
                    services.Configure<RetryProviderOptions>(context.Configuration.GetSection("RetryProviderOptions"));
                    services.Configure<WebHookReporterOptions>(context.Configuration.GetSection("WebHookReporterOptions"));
                    services.Configure<DockerMonitorOptions>(context.Configuration.GetSection("DockerMonitorOptions"));

                    services.AddLogging(x => x.AddConsole());

                    services.AddSingleton<IReporter, WebHookReporter>();
                    services.AddHttpClient();
                    services.AddSingleton<IRetryProvider, RetryProvider>();
                    services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
                    services.AddSingleton<IDockerClient, DockerClient>();
                    services.AddSingleton<IDockerApi, DockerApi>();
                    services.AddTransient<IDockerMonitor, DockerMonitor>();

                    services.AddHostedService<AlembicHost>();
                });
    }
}