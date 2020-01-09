using Alembic.Docker;
using Alembic.Docker.Client;
using Alembic.Docker.Infrastructure;
using Alembic.Docker.Reporting;
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
            var slackMessage = new SlackMessage
            {
                Text = "Alembic event",
                Blocks = new[]
                    {
                        new Block
                        {
                            Type = "section",
                            Text = new BlockText { Type = "mrkdwn", Text = "*Container ID* d38a89ab7ccfe4230ba22e9191720e20d97b92a058e642f0d79e4eb507089007" }
                        }
                    }
            };

            return CreateHostBuilder(args).Build().RunAsync();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DockerClientFactoryOptions>(context.Configuration.GetSection("DockerClientFactoryOptions"));
                    services.Configure<RetryProviderOptions>(context.Configuration.GetSection("RetryProviderOptions"));
                    services.Configure<WebHookReporterOptions>(context.Configuration.GetSection("WebHookReporterOptions"));

                    services.AddLogging(x => x.AddConsole());

                    services.AddSingleton<IReporter, WebHookReporter>();
                    services.AddHttpClient();
                    services.AddSingleton<IRetryProvider, RetryProvider>();
                    services.AddSingleton<IDockerClientFactory, DockerClientFactory>();
                    services.AddSingleton<IDockerApi, DockerApi>();
                    services.AddTransient<IDockerMonitor, DockerMonitor>();

                    services.AddHostedService<AlembicHost>();
                });
    }
}