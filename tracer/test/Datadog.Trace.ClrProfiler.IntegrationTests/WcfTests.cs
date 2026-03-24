// <copyright file="WcfTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using SkipException = Xunit.SkipException;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class WcfTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";
        private static readonly HashSet<string> ExcludeTags = ["custom-tag"];

        public WcfTests(ITestOutputHelper output)
            : base("Wcf", output)
        {
            SetServiceVersion(ServiceVersion);
            EnvironmentHelper.DebugModeEnabled = true;
        }

        public static TheoryData<string, bool, bool> GetWebHttpData() => new()
        {
            // We do support v0, but there's such a big overlap with the other
            // WCF support, it's probably not worth running all these twice here
            // Also enableWebHttpResourceNames only makes sense if enableNewWcfInstrumentation is true
            // metadataSchemaVersion, enableNewWcfInstrumentation, enableWebHttpResourceNames
            { "v0", true, false },
            { "v0", true, true },
            { "v0", false, false },
            { "v1", true, false },
            { "v1", true, true },
            { "v1", false, false },
        };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) => span.IsWcf(metadataSchemaVersion, ExcludeTags);

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [CombinatorialOrPairwiseData]
        public async Task SubmitsTraces(
            [MetadataSchemaVersionData] string metadataSchemaVersion,
            [CombinatorialValues("WSHttpBinding", "BasicHttpBinding", "NetTcpBinding", "Custom")] string binding,
            bool enableNewWcfInstrumentation,
            bool enableWcfObfuscation,
            bool useOtelClientInstrumentation)
        {
            // skip invalid combinations
            if (binding == "Custom" && !(enableNewWcfInstrumentation && enableWcfObfuscation))
            {
                throw new SkipException("Custom binding sample only supports 'New' WCF");
            }

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            SetEnvironmentVariable("DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED", enableNewWcfInstrumentation ? "true" : "false");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled, enableWcfObfuscation ? "true" : "false");

            if (useOtelClientInstrumentation)
            {
                SetEnvironmentVariable("USE_OTEL_CLIENT_INSTRUMENTATION", "1");
            }

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = binding switch
            {
                "Custom" => 26,
                "NetTcpBinding" => 44,
                "BasicHttpBinding" => 57,
                "WSHttpBinding" => 64,
                _ => throw new InvalidOperationException("Unknown binding " + binding),
            };

            using var telemetry = this.ConfigureTelemetry();
            int wcfPort = 8585;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, arguments: $"{binding} Port={wcfPort}"))
            {
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                ValidateIntegrationSpans(spans.Where(s => s.Type == SpanTypes.Web), metadataSchemaVersion, expectedServiceName: "Samples.Wcf", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings(metadataSchemaVersion, binding, enableNewWcfInstrumentation, enableWcfObfuscation, useOtelClientInstrumentation);

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseMethodName("_");

                // The custom binding doesn't trigger the integration
                await telemetry.AssertIntegrationAsync(IntegrationId.Wcf, enabled: binding != "Custom", autoEnabled: true);
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetWebHttpData))]
        public async Task WebHttp(string metadataSchemaVersion, bool enableNewWcfInstrumentation, bool enableWebHttpResourceNames)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            // When using the WebHttpBinding (not a real binding) we don't
            // care about ofuscation really, as it on(it doesn't do anything)
            SetEnvironmentVariable("DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED", enableNewWcfInstrumentation ? "true" : "false");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.WcfWebHttpResourceNamesEnabled, enableWebHttpResourceNames ? "true" : "false");

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = 16;

            using var telemetry = this.ConfigureTelemetry();
            int wcfPort = 8585;

            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, arguments: $"WebHttpBinding Port={wcfPort}"))
            {
                var spans = await agent.WaitForSpansAsync(expectedSpanCount);
                ValidateIntegrationSpans(spans.Where(s => s.Type == SpanTypes.Web), metadataSchemaVersion, expectedServiceName: "Samples.Wcf", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // The files only differ based on enableNewWcfInstrumentation
                var fileSuffix = $"{metadataSchemaVersion}.{(enableWebHttpResourceNames ? "webHttp" : "disabled")}";
                await VerifyHelper.VerifySpans(spans, settings)
                                  .DisableRequireUniquePrefix()
                                  .UseTextForParameters(fileSuffix);

                // The custom binding doesn't trigger the integration
                await telemetry.AssertIntegrationAsync(IntegrationId.Wcf, enabled: true, autoEnabled: true);
            }
        }
    }
}

#endif
