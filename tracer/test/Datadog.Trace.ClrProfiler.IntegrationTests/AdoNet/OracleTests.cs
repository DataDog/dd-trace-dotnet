// <copyright file="OracleTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AdoNet;

[Trait("RequiresDockerDependency", "true")]
[UsesVerify]
[Trait("SkipInCI", "True")] // This test requires the Oracle DB image, which is huge (8GB unpacked), so we cannot enable it without taking precautionary measures.
public class OracleTests : TracingIntegrationTest
{
    public OracleTests(ITestOutputHelper output)
        : base("OracleMDA", output)
    {
        SetServiceVersion("1.0.0");
    }

    public static IEnumerable<object[]> GetEnabledConfig()
    {
        return from metadataSchemaVersion in new[] { "v0", "v1" }
               from propagation in new[] { string.Empty, "service", "full" }
               select new[] { metadataSchemaVersion, propagation };
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    [Trait("Category", "EndToEnd")]
    [Trait("Category", "ArmUnsupported")] // the docker image used doesn't work on arm64. It can still be tested on Mac using colima, see https://github.com/abiosoft/colima
    public async Task SubmitsTraces(string metadataSchemaVersion, string dbmPropagation)
    {
        SetEnvironmentVariable("DD_DBM_PROPAGATION_MODE", dbmPropagation);
        SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
        var expectedSpanCount = 112;

        using var telemetry = this.ConfigureTelemetry();
        using var agent = EnvironmentHelper.GetMockAgent();
        using var process = await RunSampleAndWaitForExit(agent);

        var spans = agent.WaitForSpans(expectedSpanCount);

        spans.Count.Should().Be(expectedSpanCount);
        telemetry.AssertIntegrationEnabled(IntegrationId.Oracle);

        var settings = VerifyHelper.GetSpanVerifierSettings();
        // database name is generated with a random suffix
        settings.AddRegexScrubber(new Regex("oracletest[a-f0-9]{10}"), "oracletest{rand}");
        settings.AddSimpleScrubber("localhost:", "host:");
        settings.AddSimpleScrubber("oracle:", "host:");

        var fileName = nameof(OracleTests);
        await VerifyHelper.VerifySpans(spans, settings)
                          .DisableRequireUniquePrefix()
                          .UseFileName($"{fileName}.Schema{metadataSchemaVersion.ToUpper()}");
    }

    public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion)
    {
        return span.IsOracle(metadataSchemaVersion);
    }
}
