#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class MultiDomainHostTests : TestHelper
    {
        public MultiDomainHostTests(ITestOutputHelper output)
            : base("Samples.MultiDomainHost.Runner", output)
        {
            SetServiceVersion("1.0.0");
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

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "False")]
        public void WorksOutsideTheGAC(string targetFramework)
        {
            Assert.False(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was loaded from the GAC. Ensure that the assembly and its dependencies are not installed in the GAC when running this test.");

            var expectedMap = new Dictionary<string, int>()
            {
                { "DomainNeutralAssemblies.App.NoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.HttpNoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.JsonNuGetRedirects-http-client", 2 },
            };
            if (!targetFramework.StartsWith("net45"))
            {
                expectedMap.Add("DomainNeutralAssemblies.App.HttpBindingRedirects-http-client", 2);
            }

            RunSampleAndAssertAgainstExpectations(targetFramework, expectedMap);
        }

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public void WorksInsideTheGAC(string targetFramework)
        {
            Assert.True(typeof(Instrumentation).Assembly.GlobalAssemblyCache, "Datadog.Trace.ClrProfiler.Managed was not loaded from the GAC. Ensure that the assembly and its dependencies are installed in the GAC when running this test.");
            var expectedMap = new Dictionary<string, int>()
            {
                { "DomainNeutralAssemblies.App.NoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.HttpNoBindingRedirects-http-client", 2 },
                { "DomainNeutralAssemblies.App.JsonNuGetRedirects-http-client", 2 },
            };
            if (!targetFramework.StartsWith("net45"))
            {
                expectedMap.Add("DomainNeutralAssemblies.App.HttpBindingRedirects-http-client", 2);
            }

            RunSampleAndAssertAgainstExpectations(targetFramework, expectedMap);
        }

        [Theory]
        [MemberData(nameof(TargetFrameworks))]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public void DoesNotCrashInBadConfiguration(string targetFramework)
        {
            // Set bad configuration
            SetEnvironmentVariable("DD_DOTNET_TRACER_HOME", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

            int agentPort = TcpPortProvider.GetOpenPort();
            int httpPort = TcpPortProvider.GetOpenPort();

            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");
            Output.WriteLine($"Assigning port {httpPort} for the httpPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, framework: targetFramework))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");
            }
        }

        private void RunSampleAndAssertAgainstExpectations(string targetFramework, Dictionary<string, int> expectedMap)
        {
            int expectedSpanCount = expectedMap.Values.Sum();
            const string expectedOperationName = "http.request";

            var actualMap = new Dictionary<string, int>();

            int agentPort = TcpPortProvider.GetOpenPort();
            Output.WriteLine($"Assigning port {agentPort} for the agentPort.");

            using (var agent = new MockTracerAgent(agentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(agent.Port, framework: targetFramework))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(expectedSpanCount);
                Assert.True(spans.Count >= expectedSpanCount, $"Expected at least {expectedSpanCount} span, only received {spans.Count}");

                foreach (var span in spans)
                {
                    Assert.Equal(expectedOperationName, span.Name);
                    Assert.Equal(SpanTypes.Http, span.Type);
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                    // Register the service to our service<->span map
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
#endif
