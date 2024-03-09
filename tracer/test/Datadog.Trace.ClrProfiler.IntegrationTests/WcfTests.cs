// <copyright file="WcfTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class WcfTests : TracingIntegrationTest
    {
        private const string ServiceVersion = "1.0.0";

        private static readonly HashSet<string> ExcludeTags = new HashSet<string>
        {
            "custom-tag",
        };

        public WcfTests(ITestOutputHelper output)
            : base("Wcf", output)
        {
            SetServiceVersion(ServiceVersion);
        }

        public static string[] Bindings => new string[]
        {
            "WSHttpBinding",
            "BasicHttpBinding",
            "NetTcpBinding",
            "Custom",
        };

        public static IEnumerable<object[]> GetData()
        {
            foreach (var binding in Bindings)
            {
                // When using the binding example, it is expected that Old WCF fails,
                // so only test New WCF
                if (binding == "Custom")
                {
                    yield return new object[] { "v0", binding, true, true };
                    yield return new object[] { "v1", binding, true, true };
                    continue;
                }

                yield return new object[] { "v0", binding, false, true };
                yield return new object[] { "v0", binding, false, false };
                yield return new object[] { "v0", binding, true, true };
                yield return new object[] { "v0", binding, true, false };

                yield return new object[] { "v1", binding, false, true };
                yield return new object[] { "v1", binding, false, false };
                yield return new object[] { "v1", binding, true, true };
                yield return new object[] { "v1", binding, true, false };
            }
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
        [MemberData(nameof(GetData))]
        public async Task SubmitsTraces(string metadataSchemaVersion, string binding, bool enableNewWcfInstrumentation, bool enableWcfObfuscation)
        {
            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            SetEnvironmentVariable("DD_TRACE_OTEL_ENABLED", "true");

            SetEnvironmentVariable("DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED", enableNewWcfInstrumentation ? "true" : "false");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled, enableWcfObfuscation ? "true" : "false");

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = binding switch
            {
                "Custom" => 1,
                _ => 14,
            };

            using var telemetry = this.ConfigureTelemetry();
            int wcfPort = 8585;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, arguments: $"{binding} Port={wcfPort}"))
            {
                // Filter out WCF spans unrelated to the actual request handling, and filter them before returning spans
                // so we can wait on the exact number of spans we expect.
                agent.SpanFilters.Add(s => !s.Resource.Contains("schemas.xmlsoap.org") && !s.Resource.Contains("www.w3.org"));

                // The test adds a custom span to show that propagation works with WCF headers
                agent.SpanFilters.Add(s => s.Type == SpanTypes.Web || s.Type == SpanTypes.Custom);
                var spans = agent.WaitForSpans(expectedSpanCount);
                ValidateIntegrationSpans(spans.Where(s => s.Type == SpanTypes.Web), metadataSchemaVersion, expectedServiceName: "Samples.Wcf", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings(metadataSchemaVersion, binding, enableNewWcfInstrumentation, enableWcfObfuscation);

                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseMethodName("_");

                // The custom binding doesn't trigger the integration
                telemetry.AssertIntegration(IntegrationId.Wcf, enabled: binding != "Custom", autoEnabled: true);
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

            var expectedSpanCount = 5;

            using var telemetry = this.ConfigureTelemetry();
            int wcfPort = 8585;

            using var agent = EnvironmentHelper.GetMockAgent();
            using (await RunSampleAndWaitForExit(agent, arguments: $"WebHttpBinding Port={wcfPort}"))
            {
                // Filter out WCF spans unrelated to the actual request handling, and filter them before returning spans
                // so we can wait on the exact number of spans we expect.
                agent.SpanFilters.Add(s => !s.Resource.Contains("schemas.xmlsoap.org") && !s.Resource.Contains("www.w3.org"));
                // The test adds a custom span to show that propagation works with WCF headers
                agent.SpanFilters.Add(s => s.Type == SpanTypes.Web || s.Type == SpanTypes.Custom);
                var spans = agent.WaitForSpans(expectedSpanCount);
                ValidateIntegrationSpans(spans.Where(s => s.Type == SpanTypes.Web), metadataSchemaVersion, expectedServiceName: "Samples.Wcf", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings();

                // The files only differ based on enableNewWcfInstrumentation
                var fileSuffix = $"{metadataSchemaVersion}.{(enableWebHttpResourceNames ? "webHttp" : "disabled")}";
                await VerifyHelper.VerifySpans(spans, settings)
                                  .DisableRequireUniquePrefix()
                                  .UseTextForParameters(fileSuffix);

                // The custom binding doesn't trigger the integration
                telemetry.AssertIntegration(IntegrationId.Wcf, enabled: true, autoEnabled: true);
            }
        }
    }
}

#endif
