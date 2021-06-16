using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Alembic.Common.Services;
using Alembic.Docker;
using Alembic.Test.Properties;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Alembic.Test
{
    public class RestartContainerTests
    {
        private static readonly Func<(HttpStatusCode, string)> ReturnHealthy = () => (HttpStatusCode.OK, Resources.InspectContainer_ReturnHealthy);
        private static readonly Func<(HttpStatusCode, string)> ReturnNoContent = () => (HttpStatusCode.NoContent, string.Empty);
        private static readonly Func<(HttpStatusCode, string)> ReturnNotFound = () => (HttpStatusCode.NotFound, string.Empty);
        private static readonly Func<(HttpStatusCode, string)> ReturnRequestTimeout = () => (HttpStatusCode.RequestTimeout, string.Empty);

        private static readonly Func<string, Func<(HttpStatusCode, string)>, Func<(HttpStatusCode, string)>, IDockerClient> BuildDockerClientMock =
            (string id, Func<(HttpStatusCode, string)> restartResponse, Func<(HttpStatusCode, string)> inspectResponse) =>
            {
                var mock = new Mock<IDockerClient>();
                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<HttpMethod>(x => x == HttpMethod.Post),
                        It.Is<string>(x => x == $"containers/{id}/restart"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(restartResponse);

                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == $"containers/{id}/json"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(inspectResponse);

                return mock.Object;
            };

        private static readonly Func<IReporter> BuildReporterMock = () => new Mock<IReporter>().Object;

        [Fact]
        public async Task Should_Restart_Container_And_Log_Success_Message()
        {
            var loggerMock = new Mock<ILogger<DockerApi>>();

            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, ReturnNoContent, ReturnHealthy),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                loggerMock.Object);

            var status = await api.RestartContainer(Resources.InspectContainer_ReturnHealthy_Id, CancellationToken.None);

            Assert.Equal(HttpStatusCode.NoContent, status);

            Assert.Single(loggerMock.Invocations);

            var invocation = loggerMock.Invocations.First();

            Assert.Equal(LogLevel.Information, (LogLevel)invocation.Arguments[0]);

            var formattedLogValues = (IReadOnlyList<KeyValuePair<string, object>>)invocation.Arguments[2];
            Assert.Single(formattedLogValues);

            var formattedLogValue = formattedLogValues.First();
            Assert.Equal($"Container: {Resources.InspectContainer_ReturnHealthy_Id} restarted successfully.", formattedLogValue.Value);
        }

        [Fact]
        public async Task Should_Fail_To_Restart_Container_And_Log_Failure_Message()
        {
            var loggerMock = new Mock<ILogger<DockerApi>>();

            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, ReturnRequestTimeout, ReturnHealthy),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                loggerMock.Object);

            var status = await api.RestartContainer(Resources.InspectContainer_ReturnHealthy_Id, CancellationToken.None);

            Assert.Equal(HttpStatusCode.RequestTimeout, status);

            Assert.Single(loggerMock.Invocations);

            var invocation = loggerMock.Invocations.First();

            Assert.Equal(LogLevel.Warning, (LogLevel)invocation.Arguments[0]);

            var formattedLogValues = (IReadOnlyList<KeyValuePair<string, object>>)invocation.Arguments[2];
            Assert.Single(formattedLogValues);

            var formattedLogValue = formattedLogValues.First();
            Assert.Equal($"Failed to restart container: {Resources.InspectContainer_ReturnHealthy_Id}. Response status: {status} content: ", formattedLogValue.Value);
        }

        [Fact]
        public async Task Should_Fail_To_Restart_Container_And_Return_NotFound()
        {
            var loggerMock = new Mock<ILogger<DockerApi>>();

            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, null, ReturnNotFound),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                loggerMock.Object);

            var status = await api.RestartContainer(Resources.InspectContainer_ReturnHealthy_Id, CancellationToken.None);

            Assert.Equal(HttpStatusCode.NotFound, status);
        }
    }
}
