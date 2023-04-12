// <copyright file="NetActivitySdkTests.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class NetActivitySdkTests : TracingIntegrationTest
    {
        private static readonly HashSet<string> Resources = new HashSet<string>
        {
            "service.instance.id",
            "service.name",
            "service.version",
        };

        private static readonly HashSet<string> ExcludeTags = new HashSet<string>
        {
            "attribute-string",
            "attribute-int",
            "attribute-bool",
            "attribute-double",
            "attribute-stringArray",
            "attribute-stringArrayEmpty",
            "attribute-intArray",
            "attribute-intArrayEmpty",
            "attribute-boolArray",
            "attribute-boolArrayEmpty",
            "attribute-doubleArray",
            "attribute-doubleArrayEmpty",
            "set-string",
        };

        public NetActivitySdkTests(ITestOutputHelper output)
            : base("NetActivitySdk", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                "v1" => span.IsOpenTelemetryV1(Resources, ExcludeTags),
                _ => span.IsOpenTelemetryV0(Resources, ExcludeTags),
            };

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task SubmitsTraces()
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent))
            {
                const int expectedSpanCount = 25;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var myServiceNameSpans = spans.Where(s => s.Service == "MyServiceName");

                ValidateIntegrationSpans(myServiceNameSpans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(nameof(NetActivitySdkTests));

                telemetry.AssertIntegrationEnabled(IntegrationId.OpenTelemetry);
            }
        }
    }
}
