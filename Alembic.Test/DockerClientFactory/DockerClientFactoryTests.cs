using Alembic.Docker.Client;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Alembic.Test
{
    public class DockerClientFactoryTests
    {
        [Theory]
        [InlineData(".")]
        [InlineData("npipe://./pipe/docker_engine")]
        [InlineData("unix:/var/run/docker.sock")]
        public void Should_Initialize_And_Get_Client(string baseUri)
        {
            var loggerMock = new Mock<ILogger<DockerClientFactory>>();

            var factory = new DockerClientFactory(new DockerClinetFacotryOptionsEx(baseUri), loggerMock.Object);

            var client = factory.GetOrCreate();
            Assert.NotNull(client);
        }

        [Theory]
        [InlineData("http://localhost")]
        [InlineData("https://localhost")]
        [InlineData("tcp://localhost")]
        public void Should_Trhow_UnsupportedDockerClientProtocolException(string baseUri)
        {
            var loggerMock = new Mock<ILogger<DockerClientFactory>>();

            var factory = new DockerClientFactory(new DockerClinetFacotryOptionsEx(baseUri), loggerMock.Object);

            Assert.ThrowsAny<UnsupportedDockerClientProtocolException>(() => factory.GetOrCreate());
        }

        [Fact]
        public void Should_Initialize_Client_Only_Once()
        {
            var loggerMock = new Mock<ILogger<DockerClientFactory>>();

            var factory = new DockerClientFactory(new DockerClinetFacotryOptionsEx("."), loggerMock.Object);

            var client = factory.GetOrCreate();
            var client2 = factory.GetOrCreate();
            var client3 = factory.GetOrCreate();

            Assert.Same(client3, client);
            Assert.Same(client2, client);

            Assert.Single(loggerMock.Invocations);

            var arguments = loggerMock.Invocations.First().Arguments;

            Assert.Equal(LogLevel.Debug, (LogLevel)arguments[0]);

            var formattedLogValues = (IReadOnlyList<KeyValuePair<string, object>>)arguments[2];
            Assert.Single(formattedLogValues);

            var formattedLogValue = formattedLogValues.First();
            Assert.Equal($"HttpClient created: {client.BaseAddress}", formattedLogValue.Value);
        }
    }
}
