using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public class AspNetCoreMvc2LoadTests : LoadTestBase
    {
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

            foreach (var trace in traces)
            {
                var spanCount = trace.Count();
                if (spanCount >= 5)
                {
                    Output.WriteLine("Found a trace with 5 or more spans.");
                }

                Assert.True(spanCount > 10, "There is no chance there are supposed to be more than 10 spans in these traces");
            }
        }
    }
}
