using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

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
            var prefix = $"{BuildParameters.Configuration}.{BuildParameters.TargetFramework}.";
            using (var agent = new MockTracerAgent(AgentPort))
            using (var processResult = RunSampleAndWaitForExit(AgentPort, arguments: $"StackExchange {prefix}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.WaitForSpans(8).Where(s => s.Type == "redis").OrderBy(s => s.Start).ToList();

                foreach (var span in spans)
                {
                    Assert.Equal("redis.command", span.Name);
                    Assert.Equal("redis", span.Service);
                    Assert.Equal("redis", span.Type);
                    Assert.Equal("localhost", span.Tags.Get<string>("out.host"));
                    Assert.Equal("6379", span.Tags.Get<string>("out.port"));
                }

                var expected = new TupleList<string, string>
                {
                    { "SET", $"SET {prefix}StackExchange.Redis.INCR" },
                    { "PING", "PING" },
                    { "DDCUSTOM", "DDCUSTOM" },
                    { "ECHO", "ECHO" },
                    { "SLOWLOG", "SLOWLOG" },
                    { "INCR", $"INCR {prefix}StackExchange.Redis.INCR" },
                    { "INCRBYFLOAT", $"INCRBYFLOAT {prefix}StackExchange.Redis.INCR" },
                    { "TIME", "TIME" },
                };

                for (int i = 0; i < expected.Count; i++)
                {
                    var e1 = expected[i].Item1;
                    var e2 = expected[i].Item2;

                    var a1 = i < spans.Count ? spans[i].Resource : string.Empty;
                    var a2 = i < spans.Count ? spans[i].Tags.Get<string>("redis.raw_command") : string.Empty;

                    Assert.True(e1 == a1, $"invalid resource name for span {i}, {e1} != {a1}");
                    Assert.True(e2 == a2, $"invalid raw command for span {i}, {e2} != {a2}");
                }
            }
        }
    }
}
