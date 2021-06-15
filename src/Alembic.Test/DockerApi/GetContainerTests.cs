using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
    public class GetContainerTests
    {
        private static readonly Func<(HttpStatusCode, string)> ReturnTwoContainers = () => (HttpStatusCode.OK, Resources.GetContainer_ReturnTwoContainers);
        private static readonly Func<(HttpStatusCode, string)> ReturnRequestTimeout = () => (HttpStatusCode.RequestTimeout, string.Empty);
        private static readonly Func<(HttpStatusCode, string)> ReturnInvalidPayload = () => (HttpStatusCode.OK, Resources.GetContainer_ReturnInvalidPayload);

        private static readonly Func<Func<(HttpStatusCode, string)>, IDockerClient> BuildDockerClientMock =
            (Func<(HttpStatusCode, string)> getResponse) =>
            {
                var mock = new Mock<IDockerClient>();
                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<IEnumerable<ApiResponseErrorHandlingDelegate>>(x => x == Enumerable.Empty<ApiResponseErrorHandlingDelegate>()),
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == "containers/json"),
                        It.Is<string>(x => x == "all=true"),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(getResponse);

                return mock.Object;
            };

        private static readonly Func<IReporter> BuildReporterMock = () => new Mock<IReporter>().Object;

        [Fact]
        public async Task Should_Get_One_Container()
        {
            var api = new DockerApi(
                BuildDockerClientMock(ReturnTwoContainers),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var containers = await api.GetContainers(CancellationToken.None);

            Assert.Equal(2, containers.Count());

            var container = containers.First();
            var container2 = containers.Last();

            Assert.Equal(Resources.GetContainer_ReturnTwoContainers_ContainerId_1, container.Id);
            Assert.Equal(Resources.GetContainer_ReturnTwoContainers_ContainerId_2, container2.Id);
            Assert.False(string.IsNullOrWhiteSpace(container.Status));
            Assert.False(string.IsNullOrWhiteSpace(container2.Status));
        }

        [Fact]
        public async Task Should_Return_No_Container_When_DockerClient_Returns_Not_200()
        {
            var api = new DockerApi(
                BuildDockerClientMock(ReturnRequestTimeout),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var containers = await api.GetContainers(CancellationToken.None);
            Assert.Empty(containers);
        }

        [Fact]
        public async Task Should_Throw_JsonException_When_Payload_Is_Invalid()
        {
            var api = new DockerApi(
                BuildDockerClientMock(ReturnInvalidPayload),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            await Assert.ThrowsAsync<JsonException>(() => api.GetContainers(CancellationToken.None));
        }
    }
}
