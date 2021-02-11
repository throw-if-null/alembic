using Alembic.Common.Services;
using Alembic.Docker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static Alembic.Docker.DockerClient;

namespace Alembic.Test
{
    public class GetContainerTests
    {
        private static readonly Func<(HttpStatusCode, string)> ReturnOk = () => (HttpStatusCode.OK, @"[{'id': '1323d', 'Status': 'Healthy', 'Something': 'Somewhere'}]");
        private static readonly Func<(HttpStatusCode, string)> ReturnRequestTimeout = () => (HttpStatusCode.RequestTimeout, "");
        private static readonly Func<(HttpStatusCode, string)> ReturnInvalidPayload = () => (HttpStatusCode.OK, @"{'id': 'asds221'}");

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
            var api = new DockerApi(BuildDockerClientMock(ReturnOk), BuildReporterMock(), NullLogger<DockerApi>.Instance);

            var containers = await api.GetContainers(CancellationToken.None);

            Assert.Single(containers);

            var container = containers.First();
            Assert.Equal("1323d", container.Id);
            Assert.Equal("Healthy", container.Status);
        }

        [Fact]
        public async Task Should_Return_No_Container_When_DockerClient_Returns_Not_200()
        {
            var api = new DockerApi(BuildDockerClientMock(ReturnRequestTimeout), BuildReporterMock(), NullLogger<DockerApi>.Instance);

            var containers = await api.GetContainers(CancellationToken.None);
            Assert.Empty(containers);
        }

        [Fact]
        public async Task Should_Throw_JsonSerializationException_When_Payload_Is_Invalid()
        {
            var api = new DockerApi(BuildDockerClientMock(ReturnInvalidPayload), BuildReporterMock(), NullLogger<DockerApi>.Instance);

            await Assert.ThrowsAsync<JsonSerializationException>(() => api.GetContainers(CancellationToken.None));
        }
    }
}
