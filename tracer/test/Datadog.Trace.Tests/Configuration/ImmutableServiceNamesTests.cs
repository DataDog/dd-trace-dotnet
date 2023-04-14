// <copyright file="ImmutableServiceNamesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ImmutableServiceNamesTests
    {
        private const string ApplicationName = "MyApplication";
        private readonly ImmutableServiceNames _serviceNamesV0;
        private readonly ImmutableServiceNames _serviceNamesV1;

        public ImmutableServiceNamesTests()
        {
            var mappings = new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            };
            _serviceNamesV0 = new ImmutableServiceNames(mappings, "v0");
            _serviceNamesV1 = new ImmutableServiceNames(mappings, "v1");
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
            var serviceNames = new ImmutableServiceNames(new Dictionary<string, string>(), "v0");
            var expected = $"{ApplicationName}-{serviceName}";

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void DoesNotRequireAnyMappingsV1(string serviceName)
        {
            var serviceNames = new ImmutableServiceNames(new Dictionary<string, string>(), "v1");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(ApplicationName);
        }

        [Fact]
        public void CanPassNullToConstructorV0()
        {
            var serviceName = "elasticsearch";
            var expected = $"{ApplicationName}-{serviceName}";
            var serviceNames = new ImmutableServiceNames(null, "v0");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Fact]
        public void CanPassNullToConstructorV1()
        {
            var serviceName = "elasticsearch";
            var serviceNames = new ImmutableServiceNames(null, "v1");

            serviceNames.GetServiceName(ApplicationName, serviceName).Should().Be(ApplicationName);
        }

        [Fact]
        public void CanAddMappingsViaConfigurationSourceV0()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";

            var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v0" }, { ConfigurationKeys.ServiceNameMappings, $"{serviceName}:{expected}" } };
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(collection));

            var immutableTracerSettings = new ImmutableTracerSettings(tracerSettings);
            immutableTracerSettings.ServiceNameMappings.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }

        [Fact]
        public void CanAddMappingsViaConfigurationSourceV1()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";

            var collection = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v1" }, { ConfigurationKeys.ServiceNameMappings, $"{serviceName}:{expected}" } };
            var tracerSettings = new TracerSettings(new NameValueConfigurationSource(collection));

            var immutableTracerSettings = new ImmutableTracerSettings(tracerSettings);
            immutableTracerSettings.ServiceNameMappings.GetServiceName(ApplicationName, serviceName).Should().Be(expected);
        }
    }
}
