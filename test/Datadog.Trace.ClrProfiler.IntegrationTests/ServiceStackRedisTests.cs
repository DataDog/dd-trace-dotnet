using System;
using System.Linq;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ServiceStackRedisTests : TestHelper
    {
        public ServiceStackRedisTests(ITestOutputHelper output)
            : base("RedisCore", output)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces()
        {
            int agentPort = TcpPortProvider.GetOpenPort();

            var prefix = $"{BuildParameters.Configuration}.{BuildParameters.TargetFramework}.";
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agentPort, arguments: $"ServiceStack {prefix}"))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                // note: ignore the INFO command because it's timing is unpredictable (on Linux?)
                var spans = agent.WaitForSpans(11)
                                 .Where(s => s.Type == "redis" && s.Resource != "INFO")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                var host = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";

                foreach (var span in spans)
                {
                    Assert.Equal(RedisHelper.OperationName, span.Name);
                    Assert.Equal($"Samples.RedisCore-{RedisHelper.ServiceName}", span.Service);
                    Assert.Equal(SpanTypes.Redis, span.Type);
                    Assert.Equal(host, span.Tags.GetValueOrDefault("out.host"));
                    Assert.Equal("6379", span.Tags.GetValueOrDefault("out.port"));
                }

                var expected = new TupleList<string, string>
                {
                    { "ROLE", "ROLE" },
                    { "SET", $"SET {prefix}ServiceStack.Redis.INCR 0" },
                    { "PING", "PING" },
                    { "DDCUSTOM", "DDCUSTOM COMMAND" },
                    { "ECHO", "ECHO Hello World" },
                    { "SLOWLOG", "SLOWLOG GET 5" },
                    { "INCR", $"INCR {prefix}ServiceStack.Redis.INCR" },
                    { "INCRBYFLOAT", $"INCRBYFLOAT {prefix}ServiceStack.Redis.INCR 1.25" },
                    { "TIME", "TIME" },
                    { "SELECT", "SELECT 0" },
                };

                for (int i = 0; i < expected.Count; i++)
                {
                    var e1 = expected[i].Item1;
                    var e2 = expected[i].Item2;

                    var a1 = i < spans.Count
                                 ? spans[i].Resource
                                 : string.Empty;
                    var a2 = i < spans.Count
                                 ? spans[i].Tags.GetValueOrDefault("redis.raw_command")
                                 : string.Empty;

                    Assert.True(e1 == a1, $@"invalid resource name for span #{i}, expected ""{e1}"", actual ""{a1}""");
                    Assert.True(e2 == a2, $@"invalid raw command for span #{i}, expected ""{e2}"" != ""{a2}""");
                }
            }
        }
    }
}
