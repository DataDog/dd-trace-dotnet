// <copyright file="ServiceNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
        private const string ApplicationName = "MyApplication";
        private readonly ServiceNames _serviceNamesV0;
        private readonly ServiceNames _serviceNamesV1;

        public ServiceNameTests()
        {
            var mappings = new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            };
            _serviceNamesV0 = new ServiceNames(mappings, "v0");
            _serviceNamesV1 = new ServiceNames(mappings, "v1");
        }

        [Theory]
        [InlineData("sql-server", "custom-db")]
        [InlineData("http-client", "some-service")]
        [InlineData("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            _serviceNamesV0.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
            _serviceNamesV1.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNamesV0(string serviceName)
        {
            var expected = $"{ApplicationName}-{serviceName}";
            _serviceNamesV0.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNamesV1(string serviceName)
        {
            _serviceNamesV1.GetServiceName(ApplicationName, serviceName).Should().Be(ApplicationName);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void DoesNotRequireAnyMappingsV0(string serviceName)
        {
            var serviceNames = new ServiceNames(new Dictionary<string, string>(), "v0");
            var expected = $"{ApplicationName}-{serviceName}";

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void DoesNotRequireAnyMappingsV1(string serviceName)
        {
            var serviceNames = new ServiceNames(new Dictionary<string, string>(), "v1");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(ApplicationName);
        }

        [Fact]
        public void CanPassNullToConstructorV0()
        {
            var serviceName = "elasticsearch";
            var expected = $"{ApplicationName}-{serviceName}";
            var serviceNames = new ServiceNames(null, "v0");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Fact]
        public void CanPassNullToConstructorV1()
        {
            var serviceName = "elasticsearch";
            var serviceNames = new ServiceNames(null, "v1");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(ApplicationName);
        }

        [Fact]
        public void CanAddMappingsLaterV0()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";

            var serviceNames = new ServiceNames(new Dictionary<string, string>(), "v0");
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { serviceName, expected } });
            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Fact]
        public void CanAddMappingsLaterV1()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";

            var serviceNames = new ServiceNames(new Dictionary<string, string>(), "v1");
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { serviceName, expected } });
            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Fact]
        public void ReplacesExistingMappingsV0()
        {
            var serviceNames = new ServiceNames(
                new Dictionary<string, string>
                {
                    { "sql-server", "custom-db" },
                    { "elasticsearch", "original-service" },
                },
                "v0");
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { "elasticsearch", "custom-name" } });

            serviceNames.GetServiceName(ApplicationName, "mongodb").Should().Be($"{ApplicationName}-mongodb");
            serviceNames.GetServiceName(ApplicationName, "elasticsearch").Should().Be("custom-name");
            serviceNames.GetServiceName(ApplicationName, "sql-server").Should().Be($"{ApplicationName}-sql-server");
        }

        [Fact]
        public void ReplacesExistingMappingsV1()
        {
            var serviceNames = new ServiceNames(
                new Dictionary<string, string>
                {
                    { "sql-server", "custom-db" },
                    { "elasticsearch", "original-service" },
                },
                "v1");
            serviceNames.SetServiceNameMappings(new Dictionary<string, string> { { "elasticsearch", "custom-name" } });

            serviceNames.GetServiceName(ApplicationName, "mongodb").Should().Be(ApplicationName);
            serviceNames.GetServiceName(ApplicationName, "elasticsearch").Should().Be("custom-name");
            serviceNames.GetServiceName(ApplicationName, "sql-server").Should().Be(ApplicationName);
        }
    }
}
