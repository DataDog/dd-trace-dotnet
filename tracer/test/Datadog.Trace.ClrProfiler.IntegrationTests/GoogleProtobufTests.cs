// <copyright file="GoogleProtobufTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming
#pragma warning disable SA1402 // File may only contain a single type

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class GoogleProtobufTests : TracingIntegrationTest
{
    public GoogleProtobufTests(ITestOutputHelper output)
        : base("GoogleProtobuf", output)
    {
    }

    public static IEnumerable<object[]> GetEnabledConfig()
    {
        return from metadataSchemaVersion in new[] { "v0", "v1" }
               from packageVersionArray in PackageVersions.Protobuf
               select new[] { packageVersionArray[0], metadataSchemaVersion };
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion)
    {
        return span.IsProtobuf(metadataSchemaVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    public async Task TagTraces(string packageVersion, string metadataSchemaVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, "AddressBook", packageVersion))
        {
            using var assertionScope = new AssertionScope();
            var spans = await agent.WaitForSpansAsync(2);

            ValidateIntegrationSpans(spans, metadataSchemaVersion, "Samples.GoogleProtobuf", isExternalSpan: true);
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var filename = $"{nameof(GoogleProtobufTests)}";

            // Default sorting isn't very reliable, so use our own (adds in name and resource)
            await VerifyHelper.VerifySpans(
                                   spans,
                                   settings,
                                   s => s
                                       .OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                                       .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                                       .ThenBy(x => x.Name)
                                       .ThenBy(x => x.Resource)
                                       .ThenBy(x => x.Start)
                                       .ThenBy(x => x.Duration))
                              .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task NoInstrumentationForGoogleTypes()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, "TimeStamp"))
        {
            var spans = await agent.WaitForSpansAsync(2);
            foreach (var span in spans)
            {
                span.Tags.Should().NotContain(t => t.Key.StartsWith("schema."));
            }
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task OnlyEnabledWithDsm()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, null);
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, "AddressBook"))
        {
            var spans = await agent.WaitForSpansAsync(2);
            foreach (var span in spans)
            {
                span.Tags.Should().NotContain(t => t.Key.StartsWith("schema."));
            }
        }
    }
}
