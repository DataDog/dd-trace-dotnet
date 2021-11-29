// <copyright file="TelemetryDataBuilderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Telemetry;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;

namespace Datadog.Trace.Tests.Telemetry
{
    public class TelemetryDataBuilderTests
    {
        private readonly ApplicationTelemetryData _application;
        private readonly HostTelemetryData _host;

        public TelemetryDataBuilderTests()
        {
            _application = new ApplicationTelemetryData { Env = "integration-ci", ServiceName = "Test Service" };
            _host = new HostTelemetryData();
        }

        [Fact]
        public void WhenNoApplicationData_Throws()
        {
            var builder = new TelemetryDataBuilder();

            Assert.Throws<ArgumentNullException>(
                () =>
                {
                    builder.BuildTelemetryData(null, null, null, null, null);
                });
        }

        [Fact]
        public void WhenNoApplicationData_DoesNotGenerateAppClosingTelemetry()
        {
            var builder = new TelemetryDataBuilder();

            var result = builder.BuildAppClosingTelemetryData(null, null);

            result.Should().BeNull();
        }

        [Fact]
        public void WhenHasApplicationAndHostData_GeneratesAppClosingTelemetry()
        {
            var builder = new TelemetryDataBuilder();

            var result = builder.BuildAppClosingTelemetryData(_application, _host);

            result.Should().NotBeNull();
            result.Application.Should().Be(_application);
            result.SeqId.Should().Be(1);
            result.Payload.Should().BeNull();
        }

        [Fact]
        public void ShouldGenerateIncrementingIds()
        {
            var builder = new TelemetryDataBuilder();

            var data = builder.BuildTelemetryData(_application, _host, null, null, null);
            data.Should().ContainSingle().Which.SeqId.Should().Be(1);

            data = builder.BuildTelemetryData(_application, _host, null, null, null);
            data.Should().ContainSingle().Which.SeqId.Should().Be(2);

            var closingData = builder.BuildAppClosingTelemetryData(_application, _host);
            closingData.Should().NotBeNull();
            closingData.SeqId.Should().Be(3);
        }

        [Theory]
        [MemberData(nameof(TestData.Data), MemberType = typeof(TestData))]
        public void GeneratesExpectedRequestType(
            bool hasConfiguration,
            bool hasDependencies,
            bool hasIntegrations,
            string expectedRequests)
        {
            var config = hasConfiguration ? new ConfigTelemetryData() : null;
            var dependencies = hasDependencies ? new List<DependencyTelemetryData>() : null;
            var integrations = hasIntegrations ? new List<IntegrationTelemetryData>() : null;
            var expected = expectedRequests.Split(',');
            var builder = new TelemetryDataBuilder();

            var result = builder.BuildTelemetryData(_application, _host, config, dependencies, integrations);

            result.Should().NotBeNull();
            result.Select(x => x.RequestType)
                  .ToArray()
                  .Should().BeEquivalentTo(expected);

            using var scope = new AssertionScope();

            // should have incrementing IDs
            result
               .Select(x => x.SeqId)
               .Should()
               .OnlyHaveUniqueItems();

            foreach (var data in result)
            {
                data.Application.Should().Be(_application);
                data.ApiVersion.Should().NotBeNullOrEmpty();
                data.RuntimeId.Should().Be(Tracer.RuntimeId);
                data.TracerTime.Should().BeInRange(0, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

                if (data.Payload is AppDependenciesLoadedPayload appDeps)
                {
                    appDeps.Dependencies.Should().BeSameAs(dependencies);
                }
                else if (data.Payload is AppIntegrationsChangedPayload appIntegrations)
                {
                    appIntegrations.Integrations.Should().BeSameAs(integrations);
                }
                else if (data.Payload is AppStartedPayload appStarted)
                {
                    appStarted.Configuration.Should().BeSameAs(config);
                    appStarted.Dependencies.Should().BeSameAs(dependencies);
                    appStarted.Integrations.Should().BeSameAs(integrations);
                }
                else
                {
                    data.Payload.Should().BeNull();
                }
            }
        }

        public class TestData
        {
            public static TheoryData<bool, bool, bool, string> Data => new()
                // configuration, dependencies, integrations, expected request types
                {
                    { true, true, true, TelemetryRequestTypes.AppStarted },
                    { true, true, false, TelemetryRequestTypes.AppStarted },
                    { true, false, true, TelemetryRequestTypes.AppStarted },
                    { true, false, false, TelemetryRequestTypes.AppStarted },
                    { false, false, false, TelemetryRequestTypes.AppHeartbeat },
                    { false, true, false, TelemetryRequestTypes.AppDependenciesLoaded },
                    { false, false, true, TelemetryRequestTypes.AppIntegrationsChanged },
                    { false, true, true, $"{TelemetryRequestTypes.AppIntegrationsChanged},{TelemetryRequestTypes.AppDependenciesLoaded}" },
                };
        }
    }
}
