using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.ClrProfiler.IntegrationTests.Helpers;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class DomainNeutralTests : TestHelper
    {
        public DomainNeutralTests(ITestOutputHelper output)
            : base("NetFramework.DomainNeutralInstrumentationWithoutGac", output)
        {
            SetEnvironmentVariable("DD_TRACE_DOMAIN_NEUTRAL_INSTRUMENTATION", "true");
            SetServiceVersion("1.0.0");
        }

        [TargetFrameworkVersionsFact("net452;net461", Skip="Re-enable when domain-neutral instrumentation is truly tackled.")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void SubmitsTraces()
        {
            const int expectedSpanCount = 2;
            const string expectedOperationName = "http.request";
            const string expectedServiceName = "Samples.NetFramework.DomainNeutralInstrumentationWithoutGac-http-client";

            Assert.False(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was loaded from the GAC. Ensure that the assembly and its dependencies are not installed in the GAC when running this test.");

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount);
                Assert.True(spans.Count >= expectedSpanCount, $"Expected at least {expectedSpanCount} span, only received {spans.Count}");

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(expectedServiceName, span.Service);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }
            }
        }

        [TargetFrameworkVersionsFact("net452;net461")]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void DoesNotCrashInBadConfiguration()
        {
            // Set bad configuration
            SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            Assert.False(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was loaded from the GAC. Ensure that the assembly and its dependencies are not installed in the GAC when running this test.");

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"HttpClient Port={httpPort}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
            }
        }
    }
}
