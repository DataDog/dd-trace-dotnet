// <copyright file="WcfTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public class WcfTests : TestHelper
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
                // When using the binding example, it is expected that CallSite or CallTarget w/ Old WCF fails,
                // so only include CallTarget w/ New WCF
                if (binding == "Custom")
                {
                    yield return new object[] { binding, true, true };
                    continue;
                }

                yield return new object[] { binding, false, false };
                yield return new object[] { binding, true, false };
                yield return new object[] { binding, true, true };
            }
        }

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(GetData))]
        public async Task SubmitsTraces(string binding, bool enableCallTarget, bool enableNewWcfInstrumentation)
        {
            SetCallTargetSettings(enableCallTarget);
            if (enableNewWcfInstrumentation)
            {
                SetEnvironmentVariable("DD_TRACE_WCF_ENABLE_NEW_INSTRUMENTATION", "true");
            }

            Output.WriteLine("Starting WcfTests.SubmitsTraces. Starting the Samples.Wcf requires ADMIN privileges");

            var expectedSpanCount = 9;

            const string expectedOperationName = "wcf.request";

            int agentPort = TcpPortProvider.GetOpenPort();
            int wcfPort = 8585;

            using (var agent = new MockTracerAgent(agentPort))
            using (RunSampleAndWaitForExit(agent.Port, arguments: $"{binding} Port={wcfPort}"))
            {
                // Filter out WCF spans unrelated to the actual request handling, and filter them before returning spans
                // so we can wait on the exact number of spans we expect.
                agent.SpanFilters.Add(s => !s.Resource.Contains("schemas.xmlsoap.org") && !s.Resource.Contains("www.w3.org"));
                var spans = agent.WaitForSpans(expectedSpanCount, operationName: expectedOperationName);

                var settings = VerifyHelper.GetSpanVerifierSettings(binding, enableCallTarget, enableNewWcfInstrumentation);

                await Verifier.Verify(spans, settings)
                              .UseMethodName("_");
            }
        }
    }
}

#endif
