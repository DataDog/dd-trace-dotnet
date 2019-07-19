using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.LoadTests
{
    public class AspNetCoreMvc2LoadTests : LoadTestBase
    {
        private static readonly string TopLevelOperationName = "web.request";

        private static readonly string CoreMvc = "Samples.AspNetCoreMvc2";
        private static readonly string LoadTestConsole = "AspNetMvcCorePerformance";

        private static readonly int Threads = 10;
        private static readonly int IterationsPerThread = 20;

        public AspNetCoreMvc2LoadTests(ITestOutputHelper output)
            : base(output)
        {
            var aspNetCoreMvc2Port = TcpPortProvider.GetOpenPort();
            var aspNetCoreMvc2Url = GetUrl(aspNetCoreMvc2Port);
            RegisterPart(applicationName: CoreMvc, directory: "samples", requiresAgent: true, port: aspNetCoreMvc2Port);
            RegisterPart(
                applicationName: LoadTestConsole,
                directory: "reproductions",
                isAnchor: true,
                requiresAgent: false,
                commandLineArgs: new[] { aspNetCoreMvc2Url, Threads.ToString(), IterationsPerThread.ToString() });
        }

        // [Fact(Skip = "Hangs continuous integration because dotnet.exe stays around.")]
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

            var expectedTraces = Threads * IterationsPerThread;

            // Wait until the agent actually gets the spans
            mvcPart.Agent.WaitForSpans(operationName: TopLevelOperationName, count: expectedTraces);

            var spans = mvcPart.Agent.Spans;

            var traces = spans.GroupBy(s => s.TraceId).OrderByDescending(s => s.Count()).ToList();

            Assert.True(expectedTraces == traces.Count, $"We expected to receive {expectedTraces} unique traces, but we received {traces.Count}");

            foreach (var trace in traces)
            {
                var topLevelSpanCount = trace.Count(span => span.Name == TopLevelOperationName);

                if (topLevelSpanCount > 1)
                {
                    // Not good, we're nesting spans we should not be nesting
                    Assert.True(topLevelSpanCount == 1, "There should only ever be one top level span.");
                }
            }
        }
    }
}
