// <copyright file="ExporterSettingsAzureFunctionsPipeTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Specialized;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Serverless;
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
                // In Azure Functions, the constructor generates a unique name.
                // An explicit pipe name can be also specified.
                // If not in Azure Functions + compatibility layer,
                // or if no name has been specified, then
                // pipe transport is not used so TracesPipeName should be null.
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

        // Cases below exercise the SettingsManager-injected pipe-name path. The 3-arg test ctor used
        // above defaults pipeNames to None, so it can't reach the auto-generated branch.

        [Theory]
        [InlineData(null, "dd_trace_auto", "dd_trace_auto")]                   // raw unset → auto-gen wins
        [InlineData("", "dd_trace_auto", "dd_trace_auto")]                     // empty raw → auto-gen wins
        [InlineData("customer_pipe", "dd_trace_auto", "customer_pipe")]        // customer raw beats auto-gen
        public void Constructor_TracesPipeName_AutoGenAndOverride(string? explicitPipeName, string autoGen, string expected)
        {
            var config = new NameValueCollection();
            if (explicitPipeName is not null)
            {
                config.Add(ConfigurationKeys.TracesPipeName, explicitPipeName);
            }

            var source = new NameValueConfigurationSource(config);
            var telemetry = new ConfigurationTelemetry();
            var raw = new ExporterSettings.Raw(source, telemetry);
            var pipeNames = new ServerlessCompatPipeNames(TracesPipeName: autoGen, MetricsPipeName: "dd_dogstatsd_auto");
            var settings = new ExporterSettings(raw, _ => false, telemetry, pipeNames);

            settings.TracesPipeName.Should().Be(expected);
            settings.TracesTransport.Should().Be(TracesTransportType.WindowsNamedPipe);
        }

        [Theory]
        [InlineData(null, "dd_dogstatsd_auto", "dd_dogstatsd_auto")]
        [InlineData("", "dd_dogstatsd_auto", "dd_dogstatsd_auto")]
        [InlineData("customer_metrics_pipe", "dd_dogstatsd_auto", "customer_metrics_pipe")]
        public void Constructor_MetricsPipeName_AutoGenAndOverride(string? explicitPipeName, string autoGen, string expected)
        {
            var config = new NameValueCollection();
            if (explicitPipeName is not null)
            {
                config.Add(ConfigurationKeys.MetricsPipeName, explicitPipeName);
            }

            var source = new NameValueConfigurationSource(config);
            var telemetry = new ConfigurationTelemetry();
            var raw = new ExporterSettings.Raw(source, telemetry);
            var pipeNames = new ServerlessCompatPipeNames(TracesPipeName: "dd_trace_auto", MetricsPipeName: autoGen);
            var settings = new ExporterSettings(raw, _ => false, telemetry, pipeNames);

            settings.MetricsPipeName.Should().Be(expected);
            settings.MetricsTransport.Should().Be(MetricsTransportType.NamedPipe);
        }

        [Fact]
        public void Constructor_RecordsCalculatedTelemetryOrigin_OnlyForAutoGenPipeNames()
        {
            // Customer set traces pipe explicitly; metrics pipe falls through to auto-gen.
            // Only the metrics record should be tagged ConfigurationOrigins.Calculated by ExporterSettings —
            // the customer-set traces value is recorded by the Raw ctor with its real origin.
            var config = new NameValueCollection();
            config.Add(ConfigurationKeys.TracesPipeName, "customer_traces_pipe");

            var source = new NameValueConfigurationSource(config);
            var telemetry = new ConfigurationTelemetry();
            var raw = new ExporterSettings.Raw(source, telemetry);
            var pipeNames = new ServerlessCompatPipeNames(TracesPipeName: "dd_trace_auto", MetricsPipeName: "dd_dogstatsd_auto");
            _ = new ExporterSettings(raw, _ => false, telemetry, pipeNames);

            var calculatedEntries = telemetry.GetQueueForTesting()
                                             .Where(e => e.Origin == ConfigurationOrigins.Calculated)
                                             .ToList();

            calculatedEntries.Should().ContainSingle(e => e.Key == ConfigurationKeys.MetricsPipeName)
                             .Which.StringValue.Should().Be("dd_dogstatsd_auto");
            calculatedEntries.Should().NotContain(e => e.Key == ConfigurationKeys.TracesPipeName);
        }
    }
}
