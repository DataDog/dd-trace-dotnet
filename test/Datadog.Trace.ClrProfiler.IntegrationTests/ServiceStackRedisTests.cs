using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ServiceStackRedisTests : TestHelper
    {
        private const int AgentPort = 9004;

        public ServiceStackRedisTests(ITestOutputHelper output)
            : base("RedisCore", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            using (var agent = new MockTracerAgent(AgentPort))
            using (var processResult = RunSampleAndWaitForExit(AgentPort, arguments: "ServiceStack"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                var spans = agent.GetSpans().Where(s => s.Type == "redis").ToList();
                Assert.Equal(11, spans.Count);

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
                    { "ROLE", "ROLE" },
                    { "SET", "SET ServiceStack.Redis.INCR 0" },
                    { "PING", "PING" },
                    { "DDCUSTOM", "DDCUSTOM COMMAND" },
                    { "ECHO", "ECHO Hello World" },
                    { "SLOWLOG", "SLOWLOG GET 5" },
                    { "INCR", "INCR ServiceStack.Redis.INCR" },
                    { "INCRBYFLOAT", "INCRBYFLOAT ServiceStack.Redis.INCR 1.25" },
                    { "TIME", "TIME" },
                    { "SELECT", "SELECT 0" },
                    { "QUIT", "QUIT" },
                };

                for (int i = 0; i < expected.Count; i++)
                {
                    var e1 = expected[i].Item1;
                    var a1 = spans[i].Resource;

                    var e2 = expected[i].Item2;
                    var a2 = spans[i].Tags.Get<string>("redis.raw_command");

                    Assert.True(e1 == a1, $"invalid resource name for span {i}, {e1} != {a1}");
                    Assert.True(e2 == a2, $"invalid raw command for span {i}, {e2} != {a2}");
                }
            }
        }
    }
}
