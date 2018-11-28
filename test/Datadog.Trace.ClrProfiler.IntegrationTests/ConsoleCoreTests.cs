using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ConsoleCoreTests : TestHelper
    {
        private const int AgentPort = 9003;

        public ConsoleCoreTests(ITestOutputHelper output)
            : base("ConsoleCore", output)
        {
        }

        [Fact]
        public void ProfilerAttached_MethodReplaced()
        {
            using (ProcessResult processResult = RunSampleApp(AgentPort))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                dynamic output = JsonConvert.DeserializeObject(processResult.StandardOutput);
                // Assert.True((bool)output.ProfilerAttached);
                Assert.Equal(6, (int)output.AddResult);
            }
        }
    }
}
