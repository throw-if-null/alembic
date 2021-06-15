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
    public class PingTests
    {
        private static readonly Func<(HttpStatusCode, string)> ReturnPong = () => (HttpStatusCode.OK, Resources.Ping_ReturnOk);
        private static readonly Func<(HttpStatusCode, string)> ReturnRequestTimeout = () => (HttpStatusCode.RequestTimeout, string.Empty);

        private static readonly Func<string, Func<(HttpStatusCode, string)>, IDockerClient> BuildDockerClientMock =
            (string id, Func<(HttpStatusCode, string)> getResponse) =>
            {
                var mock = new Mock<IDockerClient>();
                mock
                    .Setup(x => x.MakeRequestAsync(
                        It.Is<IEnumerable<ApiResponseErrorHandlingDelegate>>(x => x == Enumerable.Empty<ApiResponseErrorHandlingDelegate>()),
                        It.Is<HttpMethod>(x => x == HttpMethod.Get),
                        It.Is<string>(x => x == "_ping"),
                        It.Is<string>(x => x == null),
                        It.Is<Dictionary<string, string>>(x => x == null),
                        It.Is<TimeSpan>(x => x == TimeSpan.FromMinutes(2)),
                        It.Is<CancellationToken>(x => x == CancellationToken.None)))
                    .ReturnsAsync(getResponse);

                return mock.Object;
            };

        private static readonly Func<IReporter> BuildReporterMock = () => new Mock<IReporter>().Object;

        [Fact]
        public async Task Should_Ping()
        {
            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, ReturnPong),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            var response = await api.Ping(CancellationToken.None);
            Assert.Equal(Resources.Ping_ReturnOk, response);
        }

        [Fact]
        public async Task Should_Throw_DockerApiException()
        {
            var api = new DockerApi(
                BuildDockerClientMock(Resources.InspectContainer_ReturnHealthy_Id, ReturnRequestTimeout),
                BuildReporterMock(),
                new ContainerRetryTracker(),
                NullLogger<DockerApi>.Instance);

            await Assert.ThrowsAsync<DockerApiException>(() => api.Ping(CancellationToken.None));
        }
    }
}
