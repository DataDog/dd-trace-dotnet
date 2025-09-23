// <copyright file="AvroTests.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

[UsesVerify]
public class AvroTests : TracingIntegrationTest
{
    public AvroTests(ITestOutputHelper output)
        : base("Avro", output)
    {
    }

    public static IEnumerable<object[]> TestData
    {
        get => from type in new[] { "Default", "SpecificDatum", "GenericDatum" }
               from packageVersionArray in PackageVersions.Avro
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { type, packageVersionArray[0], metadataSchemaVersion };
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion)
    {
        return span.IsAvro(metadataSchemaVersion);
    }

    [SkippableTheory]
    [MemberData(nameof(TestData))]
    [Trait("Category", "EndToEnd")]
    public async Task TagTraces(string type, string packageVersion, string metadataSchemaVersion)
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "1");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, type, packageVersion))
        {
            using var assertionScope = new AssertionScope();
            var spans = await agent.WaitForSpansAsync(2);

            ValidateIntegrationSpans(spans, metadataSchemaVersion, "Samples.Avro", isExternalSpan: true);
            var settings = VerifyHelper.GetSpanVerifierSettings();

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
                              .UseFileName($"{nameof(AvroTests)}.{type}.Schema{metadataSchemaVersion.ToUpper()}")
                              .DisableRequireUniquePrefix();
        }
    }

    [Fact]
    [Trait("Category", "EndToEnd")]
    public async Task OnlyEnabledWithDsm()
    {
        SetEnvironmentVariable(ConfigurationKeys.DataStreamsMonitoring.Enabled, "0");
        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using (await RunSampleAndWaitForExit(agent, "Default"))
        {
            var spans = await agent.WaitForSpansAsync(2);
            foreach (var span in spans)
            {
                span.Tags.Should().NotContain(t => t.Key.StartsWith("schema."));
            }
        }
    }
}
