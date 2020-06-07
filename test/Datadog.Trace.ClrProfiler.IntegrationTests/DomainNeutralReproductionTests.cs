using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class DomainNeutralReproductionTests : TestHelper
    {
        public DomainNeutralReproductionTests(ITestOutputHelper output)
            : base("DomainNeutralAssemblies.FileLoadException", "reproductions", output)
        {
            SetEnvironmentVariable("DD_TRACE_DOMAIN_NEUTRAL_INSTRUMENTATION", "true");
        }

        public static IEnumerable<object[]> TargetFrameworks =>
            new List<object[]>
            {
                new object[] { "net45" },
                new object[] { "net451" },
                new object[] { "net452" },
                new object[] { "net46" },
                new object[] { "net461" },
                new object[] { "net462" },
                new object[] { "net47" },
                new object[] { "net471" },
                new object[] { "net472" },
                new object[] { "net48" },
            };
#if NETFRAMEWORK

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "False")]
        public void WorksOutsideTheGAC(string targetFramework)
        {
            Assert.False(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was loaded from the GAC. Ensure that the assembly and its dependencies are not installed in the GAC when running this test.");
            MainSubRoutine(targetFramework);
        }

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public void WorksInsideTheGAC(string targetFramework)
        {
            Assert.True(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was not loaded from the GAC. Ensure that the assembly and its dependencies are installed in the GAC when running this test.");
            MainSubRoutine(targetFramework);
        }
#endif

        private void MainSubRoutine(string targetFramework)
        {
            const int expectedSpanCount = 1;
            const string expectedOperationName = "http.request";
            var expectedMap = new Dictionary<string, int>()
            {
                { "DomainNeutralAssemblies.App.NoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.HttpNoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.HttpBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.JsonNuGetRedirects-http-client", 2 }
            };
            var actualMap = new Dictionary<string, int>();

            int agentPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, framework: targetFramework))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount);
                // Assert.True(spans.Count >= expectedSpanCount, $"Expected at least {expectedSpanCount} span, only received {spans.Count}");

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                    // Regiser the service to our service<->span map
                    if (!actualMap.TryGetValue(span.Service, out int newCount))
                    {
                        newCount = 0;
                    }

                    newCount++;
                    actualMap[span.Service] = newCount;
                }

                Assert.Equal(expectedMap, actualMap);
            }
        }
    }
}
