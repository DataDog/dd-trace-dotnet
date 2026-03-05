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
                // When no explicit pipe name and no Azure Functions generated name,
                // pipe name should be null or the Azure Functions generated value
                var azureFuncName = ExporterSettings.AzureFunctionsGeneratedTracesPipeName;
                settings.TracesPipeName.Should().Be(azureFuncName);
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
                var azureFuncName = ExporterSettings.AzureFunctionsGeneratedMetricsPipeName;
                settings.MetricsPipeName.Should().Be(azureFuncName);
            }
        }

        // Note: AzureFunctionsGeneratedPipeName_IsSet_WhenInAzureFunctionsWithoutExplicitConfig
        // and AzureFunctionsGeneratedPipeName_IsNull_WhenExplicitPipeNameConfigured cannot be
        // reliably tested as unit tests because the static fields are initialized at type-load
        // time from real environment variables (WEBSITE_SITE_NAME, FUNCTIONS_WORKER_RUNTIME,
        // FUNCTIONS_EXTENSION_VERSION). By the time tests run, ExporterSettings is already loaded.
        // These scenarios are covered by the Azure Functions integration tests instead.

        [Fact]
        public void AzureFunctionsGeneratedTracesPipeName_IsNullOrValidFormat()
        {
            // We can't control the env vars at type-load time, but we can verify
            // that whatever value was generated (or not) has the correct format
            var name = ExporterSettings.AzureFunctionsGeneratedTracesPipeName;
            if (name is not null)
            {
                name.Should().StartWith("dd_trace_");
                name.Should().HaveLength("dd_trace_".Length + 32);
            }
        }

        [Fact]
        public void AzureFunctionsGeneratedMetricsPipeName_IsNullOrValidFormat()
        {
            var name = ExporterSettings.AzureFunctionsGeneratedMetricsPipeName;
            if (name is not null)
            {
                name.Should().StartWith("dd_dogstatsd_");
                name.Should().HaveLength("dd_dogstatsd_".Length + 32);
            }
        }
    }
}
