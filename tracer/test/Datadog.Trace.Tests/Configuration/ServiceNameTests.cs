// <copyright file="ServiceNameTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Configuration
{
    public class ServiceNameTests
    {
        private readonly Tracer _tracerV0;
        private readonly Tracer _tracerV1;

        public ServiceNameTests()
        {
            var mappings = new Dictionary<string, string>
            {
                { "sql-server", "custom-db" },
                { "http-client", "some-service" },
                { "mongodb", "my-mongo" },
            };

            _tracerV0 = new LockedTracer(new TracerSettings() { MetadataSchemaVersion = SchemaVersion.V0, ServiceNameMappings = mappings });
            _tracerV1 = new LockedTracer(new TracerSettings() { MetadataSchemaVersion = SchemaVersion.V1, ServiceNameMappings = mappings });
        }

        [Theory]
        [InlineData("sql-server", "custom-db")]
        [InlineData("http-client", "some-service")]
        [InlineData("mongodb", "my-mongo")]
        public void RetrievesMappedServiceNames(string serviceName, string expected)
        {
            _tracerV0.CurrentTraceSettings.GetServiceName(_tracerV0, serviceName).Should().Be(expected);
            _tracerV1.CurrentTraceSettings.GetServiceName(_tracerV1, serviceName).Should().Be(expected);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void RetrievesUnmappedServiceNames(string serviceName)
        {
            _tracerV0.CurrentTraceSettings.GetServiceName(_tracerV0, serviceName).Should().Be($"{_tracerV0.DefaultServiceName}-{serviceName}");
            _tracerV1.CurrentTraceSettings.GetServiceName(_tracerV1, serviceName).Should().Be(_tracerV1.DefaultServiceName);
        }

        [Theory]
        [InlineData("elasticsearch")]
        [InlineData("postgres")]
        [InlineData("custom-service")]
        public void DoesNotRequireAnyMappings(string serviceName)
        {
            var tracerV0 = new LockedTracer(new TracerSettings() { MetadataSchemaVersion = SchemaVersion.V0 });
            var tracerV1 = new LockedTracer(new TracerSettings() { MetadataSchemaVersion = SchemaVersion.V1 });

            tracerV0.CurrentTraceSettings.GetServiceName(tracerV0, serviceName).Should().Be($"{tracerV0.DefaultServiceName}-{serviceName}");
            tracerV1.CurrentTraceSettings.GetServiceName(tracerV1, serviceName).Should().Be(tracerV1.DefaultServiceName);
        }

        [Fact]
        public void CanAddMappingsViaConfigurationSource()
        {
            var serviceName = "elasticsearch";
            var expected = "custom-name";

            var collectionV0 = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v0" }, { ConfigurationKeys.ServiceNameMappings, $"{serviceName}:{expected}" } };
            var collectionV1 = new NameValueCollection { { ConfigurationKeys.MetadataSchemaVersion, "v1" }, { ConfigurationKeys.ServiceNameMappings, $"{serviceName}:{expected}" } };

            var tracerV0 = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collectionV0)));
            var tracerV1 = new LockedTracer(new TracerSettings(new NameValueConfigurationSource(collectionV1)));

            tracerV0.CurrentTraceSettings.GetServiceName(tracerV0, serviceName).Should().Be(expected);
            tracerV1.CurrentTraceSettings.GetServiceName(tracerV1, serviceName).Should().Be(expected);
        }

        private class LockedTracer : Tracer
        {
            internal LockedTracer(TracerSettings tracerSettings)
                : base(new LockedTracerManager(tracerSettings))
            {
            }
        }

        private class LockedTracerManager : TracerManager, ILockedTracer
        {
            public LockedTracerManager(TracerSettings tracerSettings)
                : base(new ImmutableTracerSettings(tracerSettings), null, Mock.Of<IScopeManager>(), null, null, null, null, null, null, null, null, null, null, null)
            {
            }
        }
    }
}
