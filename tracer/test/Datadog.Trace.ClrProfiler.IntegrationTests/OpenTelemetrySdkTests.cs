// <copyright file="OpenTelemetrySdkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
    public class OpenTelemetrySdkTests : TracingIntegrationTest
    {
        private readonly string customServiceName = "CustomServiceName";

        public OpenTelemetrySdkTests(ITestOutputHelper output)
            : base("OpenTelemetrySdk", output)
        {
            SetServiceName(customServiceName);
            SetServiceVersion(string.Empty);
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                _ => span.IsOpenTelemetry(
                    resources: new HashSet<string>
                    {
                        "service.instance.id",
                        "service.name",
                        "service.version"
                    },
                    excludeTags: new HashSet<string>
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
                    }),
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public async Task SubmitsTraces(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedSpanCount = 12;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);

                var otelSpans = spans.Where(s => s.Service == "MyServiceName");
                var activitySourceSpans = spans.Where(s => s.Service == customServiceName);

                otelSpans.Count().Should().Be(expectedSpanCount - 1);
                activitySourceSpans.Count().Should().Be(1);

                ValidateIntegrationSpans(otelSpans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);
                ValidateIntegrationSpans(activitySourceSpans, metadataSchemaVersion: "v0", expectedServiceName: customServiceName, isExternalSpan: false);

                // there's a bug in < 1.2.0 where they get the span parenting wrong
                // so use a separate snapshot
                var filename = nameof(OpenTelemetrySdkTests) + GetSuffix(packageVersion);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(filename)
                                  .DisableRequireUniquePrefix();

                telemetry.AssertIntegrationEnabled(IntegrationId.OpenTelemetry);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public async Task SubmitsTracesWithActivitySource(string packageVersion)
        {
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");
            SetEnvironmentVariable("ADD_ADDITIONAL_ACTIVITY_SOURCE", "true");

            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                const int expectedSpanCount = 12;
                var spans = agent.WaitForSpans(expectedSpanCount);

                using var s = new AssertionScope();
                spans.Count.Should().Be(expectedSpanCount);
                ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "MyServiceName", isExternalSpan: false);

                // there's a bug in < 1.2.0 where they get the span parenting wrong
                // so use a separate snapshot
                var filename = nameof(OpenTelemetrySdkTests) + "WithActivitySource" + GetSuffix(packageVersion);

                var settings = VerifyHelper.GetSpanVerifierSettings();
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(filename)
                                  .DisableRequireUniquePrefix();

                telemetry.AssertIntegrationEnabled(IntegrationId.OpenTelemetry);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(PackageVersions.OpenTelemetry), MemberType = typeof(PackageVersions))]
        public void IntegrationDisabled(string packageVersion)
        {
            using (var telemetry = this.ConfigureTelemetry())
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(1, 2000);

                using var s = new AssertionScope();
                spans.Should().BeEmpty();
                telemetry.AssertIntegrationDisabled(IntegrationId.OpenTelemetry);
            }
        }

        private static string GetSuffix(string packageVersion)
        {
            // The snapshots are only different in .NET Core 2.1 - .NET 5 with package version 1.0.1
#if !NET6_0_OR_GREATER
            if (!string.IsNullOrEmpty(packageVersion)
             && new Version(packageVersion) < new Version("1.2.0"))
            {
                return "_1_0";
            }
#endif

            return string.Empty; // default is >= 1.2.0
        }
    }
}
