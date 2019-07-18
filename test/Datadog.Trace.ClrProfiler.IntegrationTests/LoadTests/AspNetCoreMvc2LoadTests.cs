using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public class AspNetCoreMvc2LoadTests : LoadTestBase
    {
        private static readonly string TopLevelOperationName = "web.request";

        private const string CoreMvc = "Samples.AspNetCoreMvc2";
        private const string LoadTestConsole = "AspNetMvcCorePerformance";

        public AspNetCoreMvc2LoadTests(ITestOutputHelper output)
            : base(output)
        {
            var aspNetCoreMvc2Port = TcpPortProvider.GetOpenPort();
            var aspNetCoreMvc2Url = GetUrl(aspNetCoreMvc2Port);
            RegisterPart(applicationName: CoreMvc, directory: "samples", requiresAgent: true, port: aspNetCoreMvc2Port);
            RegisterPart(applicationName: LoadTestConsole, directory: "reproductions", isAnchor: true, requiresAgent: false, commandLineArgs: new[] { aspNetCoreMvc2Url });
        }

        [Fact]
        [Trait("Category", "Load")]
        public void RunLoadTest_WithVerifications()
        {
            if (!EnvironmentHelper.IsCoreClr())
            {
                Output.WriteLine("Ignored for .NET Framework");
                return;
            }

            var loadTestParts = RunAllParts();

            var consolePart = loadTestParts.Single(i => i.Application == LoadTestConsole);
            var mvcPart = loadTestParts.Single(i => i.Application == CoreMvc);

            var spans = mvcPart.Agent.Spans;

            var traces = spans.GroupBy(s => s.TraceId);

            var maximumSpansPerTrace = 8;

            foreach (var trace in traces)
            {
                var spanCount = trace.Count();

                if (spanCount <= 2)
                {
                    // Looking pretty normal here
                    continue;
                }

                if (spanCount >= 5)
                {
                    Output.WriteLine("Found a trace with 5 or more spans.");
                }

                var topLevelSpanCount = trace.Count(span => span.Name == TopLevelOperationName);

                if (topLevelSpanCount > 1)
                {
                    // Not good, we're nesting spans we should be nesting
                    Assert.True(topLevelSpanCount == 1, "There should only ever be one top level span.");
                }

                Assert.True(spanCount <= maximumSpansPerTrace, $"There should be no more than {maximumSpansPerTrace} per trace.");
            }
        }
    }
}
