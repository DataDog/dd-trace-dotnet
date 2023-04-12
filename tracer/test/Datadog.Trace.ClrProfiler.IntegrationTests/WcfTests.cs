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
                    yield return new object[] { binding, true, true };
                    continue;
                }

                yield return new object[] { binding, false, true };
                yield return new object[] { binding, false, false };
                yield return new object[] { binding, true,  true };
                yield return new object[] { binding, true,  false };
            }
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            metadataSchemaVersion switch
            {
                _ => span.IsWcf(),
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetData))]
        public async Task SubmitsTraces(string binding, bool enableNewWcfInstrumentation, bool enableWcfObfuscation)
        {
            if (enableNewWcfInstrumentation)
            {
                SetEnvironmentVariable("DD_TRACE_DELAY_WCF_INSTRUMENTATION_ENABLED", "true");
            }

            if (enableWcfObfuscation)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled, "true");
            }
            else
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled, "false");
            }

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = 6;
            const string expectedOperationName = "wcf.request";

            using var telemetry = this.ConfigureTelemetry();
            int wcfPort = 8585;

            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"{binding} Port={wcfPort}"))
            {
                // Filter out WCF spans unrelated to the actual request handling, and filter them before returning spans
                // so we can wait on the exact number of spans we expect.
                agent.SpanFilters.Add(s => !s.Resource.Contains("schemas.xmlsoap.org") && !s.Resource.Contains("www.w3.org"));
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);
                ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.Wcf", isExternalSpan: false);

                var settings = VerifyHelper.GetSpanVerifierSettings(binding, enableNewWcfInstrumentation, enableWcfObfuscation);

                await VerifyHelper.VerifySpans(spans, settings)
                              .UseMethodName("_");

                // The custom binding doesn't trigger the integration
                telemetry.AssertIntegration(IntegrationId.Wcf, enabled: binding != "Custom", autoEnabled: true);
            }
        }
    }
}

#endif
