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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using static Alembic.Docker.DockerClient;

namespace Alembic.Test
{
    public class InspectContainer
    {
        private static readonly Func<(HttpStatusCode, string)> ReturnHealthy = () => (HttpStatusCode.OK, Resources.InspectContainer_ReturnHealthy);
        private static readonly Func<(HttpStatusCode, string)> ReturnUnhealthy = () => (HttpStatusCode.OK, Resources.InspectContainer_ReturnUnhealthy);
        private static readonly Func<(HttpStatusCode, string)> ReturnNotFound = () => (HttpStatusCode.NotFound, string.Empty);

        private static readonly Func<string, Func<(HttpStatusCode, string)>, IDockerClient> BuildDockerClientMock =
            (string id, Func<(HttpStatusCode, string)> getResponse) =>
            {
                var mock = new Mock<IDockerClient>();
                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<IEnumerable<ApiResponseErrorHandlingDelegate>>(x => x == Enumerable.Empty<ApiResponseErrorHandlingDelegate>()),
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == $"containers/{id}/json"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(getResponse);

                return mock.Object;
            };

        private static readonly Func<IReporter> BuildReporterMock = () => new Mock<IReporter>().Object;

        [Fact]
        public async Task Should_Inspect_Healthy_Container()
        {
            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, ReturnHealthy),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var container = await api.InspectContainer(Resources.InspectContainer_ReturnHealthy_Id, CancellationToken.None);

            Assert.Equal(Resources.InspectContainer_ReturnHealthy_Id, container.Id);
            Assert.Equal("healthy", container.State.Health.Status);
        }

        [Fact]
        public async Task Should_Inspect_Unhealthy_Container()
        {
            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnUnhealthy_Id, ReturnUnhealthy),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var container = await api.InspectContainer(Resources.InspectContainer_ReturnUnhealthy_Id, CancellationToken.None);

            Assert.Equal(Resources.InspectContainer_ReturnUnhealthy_Id, container.Id);
            Assert.Equal("starting", container.State.Health.Status);
            Assert.Equal(2, container.State.Health.FailingStreak);
            Assert.True(container.State.Health.Log.Length > 1);
        }

        [Fact]
        public async Task Should_Return_Null_When_COntainer_Is_Not_Found()
        {
            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnUnhealthy_Id, ReturnNotFound),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var container = await api.InspectContainer(Resources.InspectContainer_ReturnUnhealthy_Id, CancellationToken.None);
            Assert.Null(container);
        }
    }
}
