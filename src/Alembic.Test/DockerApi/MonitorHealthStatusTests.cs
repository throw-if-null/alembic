using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alembic.Common.Contracts;
using Alembic.Common.Services;
using Alembic.Docker;
using Alembic.Test.Properties;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Alembic.Test
{
    public class MonitorHealthStatusTests
    {
        private static readonly Func<Stream> StatusStream_ReturnUnhealthy = () => new MemoryStream(Encoding.UTF8.GetBytes(Resources.MonitorHealthStatus_Unhealthy));
        private static readonly Func<Stream> StatusStream_ReturnHealthy = () => new MemoryStream(Encoding.UTF8.GetBytes(Resources.MonitorHealthStatus_Healthy));
        private static readonly Func<Stream> StatusStream_ReturnUnhealthyThreeTimes = () => new MemoryStream(Encoding.UTF8.GetBytes(Resources.MonitorHealthStatus_Unhealthy_ThreeTimes));
        private static readonly Func<(HttpStatusCode, string)> Inspect_ReturnUnhealthy = () => (HttpStatusCode.OK, Resources.InspectContainer_ReturnUnhealthy);
        private static readonly Func<(HttpStatusCode, string)> ReturnNoContent = () => (HttpStatusCode.NoContent, string.Empty);

        private static readonly Func<string, Func<Stream>, Func<(HttpStatusCode, string)>, Func<(HttpStatusCode, string)>, Func<(HttpStatusCode, string)>, IDockerClient> BuildDockerClientMock =
            (string id, Func<Stream> streamResponse, Func<(HttpStatusCode, string)> inspectResponse, Func<(HttpStatusCode, string)> restartResponse, Func<(HttpStatusCode, string)> killResponse) =>
            {
                var mock = new Mock<IDockerClient>();
                mock
                    .Setup(x => x.MakeRequestForStreamAsync(
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == "events"),
                        It.Is<string>(x => x == @"filters=%7B%22event%22%3A%7B%22health_status%22%3Atrue%7D%7D"),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(streamResponse);

                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == $"containers/{id}/json"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(inspectResponse);

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
                        It.Is<HttpMethod>(x => x == HttpMethod.Post),
                        It.Is<string>(x => x == $"containers/{id}/kill"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(killResponse);

                return mock.Object;
            };

        private static readonly Func<IReporter> BuildReporterMock = () => new Mock<IReporter>().Object;

        [Fact]
        public async Task Should_Get_Status_And_Restart()
        {
            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnUnhealthy,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            await api.MonitorHealthStatus(CancellationToken.None, 3, true, OnUnhealthyStatusReceived);

            static void OnUnhealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(1, report.RestartCount);
                Assert.True(report.Restarted);
                Assert.False(report.Killed);
            }
        }

        [Fact]
        public async Task Should_Get_Status_And_Restart_Again()
        {
            var retryTracker = new ContainerRetryTracker();
            retryTracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);

            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnUnhealthy,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                retryTracker,
                NullLogger<DockerApi>.Instance);

            await api.MonitorHealthStatus(CancellationToken.None, 3, true, OnUnhealthyStatusReceived);

            static void OnUnhealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(2, report.RestartCount);
                Assert.True(report.Restarted);
                Assert.False(report.Killed);
            }
        }

        [Fact]
        public async Task Should_Do_Nothing()
        {
            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnHealthy,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            await api.MonitorHealthStatus(CancellationToken.None, 3, true, OnHealthyStatusReceived);

            static void OnHealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(0, report.RestartCount);
                Assert.False(report.Restarted);
                Assert.False(report.Killed);
            }
        }

        [Fact]
        public async Task Should_Get_Status_And_Kill()
        {
            var retryTracker = new ContainerRetryTracker();
            retryTracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);
            retryTracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);
            retryTracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);

            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnUnhealthy,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                retryTracker,
                NullLogger<DockerApi>.Instance);

            await api.MonitorHealthStatus(CancellationToken.None, 3, true, OnUnhealthyStatusReceived);

            static void OnUnhealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(4, report.RestartCount);
                Assert.False(report.Restarted);
                Assert.True(report.Killed);
            }
        }

        [Fact]
        public async Task Should_Get_Status_And_Skip_Kill()
        {
            var retrytracker = new ContainerRetryTracker();
            retrytracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);
            retrytracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);
            retrytracker.Add(Resources.InspectContainer_ReturnUnhealthy_Id);

            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnUnhealthy,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                retrytracker,
                NullLogger<DockerApi>.Instance);

            await api.MonitorHealthStatus(CancellationToken.None, 3, false, OnUnhealthyStatusReceived);

            static void OnUnhealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(4, report.RestartCount);
                Assert.False(report.Restarted);
                Assert.False(report.Killed);
            }
        }

        [Fact]
        public async Task Should_Kill_After_Rester_Has_Retried_Out()
        {

            var api = new DockerApi(
                BuildDockerClientMock(
                    Resources.InspectContainer_ReturnUnhealthy_Id,
                    StatusStream_ReturnUnhealthyThreeTimes,
                    Inspect_ReturnUnhealthy,
                    ReturnNoContent,
                    ReturnNoContent),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            int counter = 0;
            await api.MonitorHealthStatus(CancellationToken.None, 3, true, OnUnhealthyStatusReceived);

            void OnUnhealthyStatusReceived(UnhealthyStatusActionReport report)
            {
                Assert.NotNull(report);
                Assert.Equal(++counter, report.RestartCount);
                if (report.RestartCount > 3)
                {
                    Assert.False(report.Restarted);
                    Assert.True(report.Killed);
                }
                else
                {
                    Assert.True(report.Restarted);
                    Assert.False(report.Killed);
                }
            }
        }
    }
}
