using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ServiceStackRedisTests : TestHelper
    {
        public ServiceStackRedisTests(ITestOutputHelper output)
            : base("ServiceStack.Redis", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetServiceStackRedisData()
        {
            foreach (object[] item in PackageVersions.ServiceStackRedis)
            {
                yield return item.Concat(new object[] { false, false, }).ToArray();
                yield return item.Concat(new object[] { true, false, }).ToArray();
                yield return item.Concat(new object[] { true, true, }).ToArray();
            }
        }

        [Theory]
        [MemberData(nameof(GetServiceStackRedisData))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion, bool enableCallTarget, bool enableInlining)
        {
            SetCallTargetSettings(enableCallTarget, enableInlining);

            int agentPort = TcpPortProvider.GetOpenPort();

            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode}");

                // note: ignore the INFO command because it's timing is unpredictable (on Linux?)
                var spans = agent.WaitForSpans(11)
                                 .Where(s => s.Type == "redis" && s.Resource != "INFO" && s.Resource != "ROLE" && s.Resource != "QUIT")
                                 .OrderBy(s => s.Start)
                                 .ToList();

                var host = Environment.GetEnvironmentVariable("SERVICESTACK_REDIS_HOST") ?? "localhost:6379";
                var port = host.Substring(host.IndexOf(':') + 1);
                host = host.Substring(0, host.IndexOf(':'));

                foreach (var span in spans)
                {
                    Assert.Equal("redis.command", span.Name);
                    Assert.Equal("Samples.ServiceStack.Redis-redis", span.Service);
                    Assert.Equal(SpanTypes.Redis, span.Type);
                    Assert.Equal(host, DictionaryExtensions.GetValueOrDefault(span.Tags, "out.host"));
                    Assert.Equal(port, DictionaryExtensions.GetValueOrDefault(span.Tags, "out.port"));
                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                }

                var expectedFromOneRun = new TupleList<string, string>
                {
                    { "SET", $"SET {TestPrefix}ServiceStack.Redis.INCR 0" },
                    { "PING", "PING" },
                    { "DDCUSTOM", "DDCUSTOM COMMAND" },
                    { "ECHO", "ECHO Hello World" },
                    { "SLOWLOG", "SLOWLOG GET 5" },
                    { "INCR", $"INCR {TestPrefix}ServiceStack.Redis.INCR" },
                    { "INCRBYFLOAT", $"INCRBYFLOAT {TestPrefix}ServiceStack.Redis.INCR 1.25" },
                    { "TIME", "TIME" },
                    { "SELECT", "SELECT 0" },
                };

                var expected = new TupleList<string, string>();
                expected.AddRange(expectedFromOneRun);
                expected.AddRange(expectedFromOneRun);
#if NETCOREAPP3_1 || NET5_0
                expected.AddRange(expectedFromOneRun); // On .NET Core 3.1 and .NET 5 we run the routine a third time
#endif

                Assert.Equal(expected.Count, spans.Count);

                for (int i = 0; i < expected.Count; i++)
                {
                    var e1 = expected[i].Item1;
                    var e2 = expected[i].Item2;

                    var a1 = i < spans.Count
                                 ? spans[i].Resource
                                 : string.Empty;
                    var a2 = i < spans.Count
                                 ? DictionaryExtensions.GetValueOrDefault(spans[i].Tags, "redis.raw_command")
                                 : string.Empty;

                    Assert.True(e1 == a1, $@"invalid resource name for span #{i}, expected ""{e1}"", actual ""{a1}""");
                    Assert.True(e2 == a2, $@"invalid raw command for span #{i}, expected ""{e2}"" != ""{a2}""");
                }
            }
        }
    }
}
