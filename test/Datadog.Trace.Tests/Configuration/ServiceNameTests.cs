using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
        private const string ApplicationName = "MyApplication";
        private readonly ServiceNames _serviceNames;

        public ServiceNameTests()
        {
            _serviceNames = new ServiceNames(new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            });
        }

        [Theory]
        [InlineData("sql-server", "custom-db")]
        [InlineData("http-client", "some-service")]
        [InlineData("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            var actual = _serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNames(string serviceName)
        {
            var expected = $"{ApplicationName}-{serviceName}";

            var actual = _serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void DoesNotRequireAnyMappings(string serviceName)
        {
            var serviceNames = new ServiceNames(new Dictionary<string, string>());
            var expected = $"{ApplicationName}-{serviceName}";

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanPassNullToConstructor()
        {
            var serviceName = "elasticsearch";
            var expected = $"{ApplicationName}-{serviceName}";
            var serviceNames = new ServiceNames(null);

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CanAddMappingsLater()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";
            var serviceNames = new ServiceNames(new Dictionary<string, string>());
            serviceNames.SetServiceNameMapping(serviceName, expected);

            var actual = serviceNames.GetServiceName(ApplicationName, serviceName);

            Assert.Equal(expected, actual);
        }
    }
}
