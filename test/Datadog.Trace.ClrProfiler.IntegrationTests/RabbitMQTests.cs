#if !NET452
using System;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class RabbitMQTests : TestHelper
    {
        private const string ExpectedServiceName = "Samples.RabbitMQ-rabbitmq";

        public RabbitMQTests(ITestOutputHelper output)
            : base("RabbitMQ", output)
        {
            SetServiceVersion("1.0.0");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.RabbitMQ), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
            var expectedSpanCount = 24;

            int agentPort = TcpPortProvider.GetOpenPort();
            using (var agent = new MockTracerAgent(agentPort))
            using (var processResult = RunSampleAndWaitForExit(agent.Port, packageVersion: packageVersion))
            {
                Assert.True(processResult.ExitCode >= 0, $"Process exited with code {processResult.ExitCode} and exception: {processResult.StandardError}");

                var spans = agent.WaitForSpans(expectedSpanCount); // Do not filter on operation name because they will vary depending on instrumented method
                Assert.True(spans.Count >= expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                foreach (var span in spans)
                {
                    if (string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.Equal(SpanTypes.MessageClient, span.Type);
                        Assert.Equal("RabbitMQ", span.Tags[Tags.InstrumentationName]);
                        Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");
                        Assert.NotNull(span.Tags[Tags.AmqpCommand]);
                        Assert.Equal("amqp.command", span.Name);

                        var command = span.Tags[Tags.AmqpCommand];

                        if (command.StartsWith("basic.", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.Equals(command, "basic.publish", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.Equal(SpanKinds.Producer, span.Tags[Tags.SpanKind]);
                                Assert.NotNull(span.Tags[Tags.AmqpExchange]);
                                Assert.NotNull(span.Tags[Tags.AmqpRoutingKey]);

                                Assert.NotNull(span.Tags["message.size"]);
                                Assert.True(int.TryParse(span.Tags["message.size"], out _));

                                // Enforce that the resource name has the following structure: "basic.publish [<default>|{actual exchangeName}] -> [<all>|<generated>|{actual routingKey}]"
                                string regexPattern = @"basic\.publish (?<exchangeName>\S*) -> (?<routingKey>\S*)";
                                var match = Regex.Match(span.Resource, regexPattern);
                                Assert.True(match.Success);

                                var exchangeName = match.Groups["exchangeName"].Value;
                                Assert.True(string.Equals(exchangeName, "<default>") || string.Equals(exchangeName, span.Tags[Tags.AmqpExchange]));

                                var routingKey = match.Groups["routingKey"].Value;
                                Assert.True(string.Equals(routingKey, "<all>") || string.Equals(routingKey, "<generated>") || string.Equals(routingKey, span.Tags[Tags.AmqpRoutingKey]));
                            }
                            else if (string.Equals(command, "basic.get", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.NotNull(span.ParentId);
                                Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                                Assert.NotNull(span.Tags[Tags.AmqpQueue]);

                                Assert.NotNull(span.Tags["message.size"]);
                                Assert.True(int.TryParse(span.Tags["message.size"], out _));

                                // Enforce that the resource name has the following structure: "basic.get [<generated>|{actual queueName}]"
                                string regexPattern = @"basic\.get (?<queueName>\S*)";
                                var match = Regex.Match(span.Resource, regexPattern);
                                Assert.True(match.Success);

                                var queueName = match.Groups["queueName"].Value;
                                Assert.True(string.Equals(queueName, "<generated>") || string.Equals(queueName, span.Tags[Tags.AmqpQueue]));
                            }
                            else if (string.Equals(command, "basic.deliver", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.NotNull(span.ParentId);
                                Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                                // Assert.NotNull(span.Tags[Tags.AmqpQueue]); // Java does this but we're having difficulty doing this. Push to v2?
                                Assert.NotNull(span.Tags[Tags.AmqpExchange]);
                                Assert.NotNull(span.Tags[Tags.AmqpRoutingKey]);

                                Assert.NotNull(span.Tags["message.size"]);
                                Assert.True(int.TryParse(span.Tags["message.size"], out _));

                                // Enforce that the resource name has the following structure: "basic.deliver [<generated>|{actual queueName}]"
                                string regexPattern = @"basic\.deliver (?<queueName>\S*)";
                                var match = Regex.Match(span.Resource, regexPattern);
                                // Assert.True(match.Success); // Enable once we can get the queue name included

                                var queueName = match.Groups["queueName"].Value;
                                // Assert.True(string.Equals(queueName, "<generated>") || string.Equals(queueName, span.Tags[Tags.AmqpQueue])); // Enable once we can get the queue name included
                            }
                            else
                            {
                                throw new Xunit.Sdk.XunitException($"amqp.command {command} not recognized.");
                            }
                        }
                        else
                        {
                            Assert.Equal(SpanKinds.Client, span.Tags[Tags.SpanKind]);
                            Assert.Equal(command, span.Resource);

                            if (string.Equals(command, "exchange.declare", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.NotNull(span.Tags[Tags.AmqpExchange]);
                            }
                            else if (string.Equals(command, "queue.declare", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.NotNull(span.Tags[Tags.AmqpQueue]);
                            }
                            else if (string.Equals(command, "queue.bind", StringComparison.OrdinalIgnoreCase))
                            {
                                Assert.NotNull(span.Tags[Tags.AmqpExchange]);
                                Assert.NotNull(span.Tags[Tags.AmqpQueue]);
                                Assert.NotNull(span.Tags[Tags.AmqpRoutingKey]);
                            }
                            else
                            {
                                throw new Xunit.Sdk.XunitException($"amqp.command {command} not recognized.");
                            }
                        }
                    }
                    else
                    {
                        // These are manual traces
                        Assert.Equal("Samples.RabbitMQ", span.Service);
                        Assert.Equal("1.0.0", span.Tags?.GetValueOrDefault(Tags.Version));
                    }
                }
            }
        }
    }
}
#endif
