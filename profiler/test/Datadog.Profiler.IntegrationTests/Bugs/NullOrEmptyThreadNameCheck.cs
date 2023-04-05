using Datadog.Profiler.IntegrationTests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Profiler.IntegrationTests.Bugs
{
    public class NullOrEmptyThreadNameCheck
    {
        private const string ScenarioNullOrEmptyThreadName = "--scenario 19";

        private readonly ITestOutputHelper _output;

        public NullOrEmptyThreadNameCheck(ITestOutputHelper output)
        {
            _output = output;
        }

        [TestAppFact("Samples.Computer01")]
        public void ShouldNotCrashWhenNullOrEmptyThreadName(string appName, string framework, string appAssembly)
        {
            var runner = new TestApplicationRunner(appName, framework, appAssembly, _output, commandLine: ScenarioNullOrEmptyThreadName);
            runner.Environment.SetVariable(EnvironmentVariables.WallTimeProfilerEnabled, "1");
            runner.Environment.SetVariable(EnvironmentVariables.CpuProfilerEnabled, "1");

            using var agent = MockDatadogAgent.CreateHttpAgent(_output);
            runner.Run(agent);

            // we should not see any crash
            Assert.True(true);
        }
    }
}
