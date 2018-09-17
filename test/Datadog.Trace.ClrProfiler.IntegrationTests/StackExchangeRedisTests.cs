using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

#if !NET452

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class StackExchangeRedisTests : TestHelper
    {
        private const int AgentPort = 9003;

        public StackExchangeRedisTests(ITestOutputHelper output)
            : base("RedisCore", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (ProcessResult processResult = RunSampleAndWaitForExit(AgentPort))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.GetSpans();
                Assert.True(spans.Count > 0, "expected at least one span");
                foreach (var span in spans)
                {
                    Assert.Equal("redis.command", span.Name);
                    Assert.Equal("redis", span.Service);
                    Assert.Equal("redis", span.Type);
                    Assert.Equal("localhost", span.Tags.Get<string>("out.host"));
                    Assert.Equal("6379", span.Tags.Get<string>("out.port"));
                    Assert.True(span.Resource == "GET" || span.Resource == "SET", "resource should be set to command name");
                    Assert.NotEmpty(span.Tags.Get<string>("redis.raw_command"));
                }
            }
        }
    }
}

#endif
