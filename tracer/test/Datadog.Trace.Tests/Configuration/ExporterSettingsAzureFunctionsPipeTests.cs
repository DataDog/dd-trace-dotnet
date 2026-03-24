// <copyright file="ExporterSettingsAzureFunctionsPipeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using FluentAssertions;
using Xunit;
using MetricsTransportType = Datadog.Trace.Vendors.StatsdClient.Transport.TransportType;

namespace Datadog.Trace.Tests.Configuration
{
    public class ExporterSettingsAzureFunctionsPipeTests
    {
        [Theory]
        [InlineData("my_explicit_pipe", "my_explicit_pipe")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Constructor_SelectsCorrectTracesPipeName(string? explicitPipeName, string? expectedPipeName)
        {
            var config = new NameValueCollection();
            if (explicitPipeName is not null)
            {
                config.Add(ConfigurationKeys.TracesPipeName, explicitPipeName);
            }

            var source = new NameValueConfigurationSource(config);
            var settings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

            if (expectedPipeName is not null)
            {
                settings.TracesPipeName.Should().Be(expectedPipeName);
                settings.TracesTransport.Should().Be(TracesTransportType.WindowsNamedPipe);
            }
            else
            {
                // When no explicit pipe name is configured and not in Azure Functions,
                // pipe transport is not used so TracesPipeName should be null.
                // In Azure Functions, the constructor generates a unique name which
                // is covered by integration tests.
                settings.TracesPipeName.Should().BeNull();
            }
        }

        [Theory]
        [InlineData("my_explicit_pipe", "my_explicit_pipe")]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void Constructor_SelectsCorrectMetricsPipeName(string? explicitPipeName, string? expectedPipeName)
        {
            var config = new NameValueCollection();
            if (explicitPipeName is not null)
            {
                config.Add(ConfigurationKeys.MetricsPipeName, explicitPipeName);
            }

            var source = new NameValueConfigurationSource(config);
            var settings = new ExporterSettings(source, _ => false, NullConfigurationTelemetry.Instance);

            if (expectedPipeName is not null)
            {
                settings.MetricsPipeName.Should().Be(expectedPipeName);
                settings.MetricsTransport.Should().Be(MetricsTransportType.NamedPipe);
            }
            else
            {
                // When no explicit pipe name is configured and not in Azure Functions,
                // pipe transport is not used so MetricsPipeName should be null.
                // In Azure Functions, the constructor generates a unique name which
                // is covered by integration tests.
                settings.MetricsPipeName.Should().BeNull();
            }
        }
    }
}
