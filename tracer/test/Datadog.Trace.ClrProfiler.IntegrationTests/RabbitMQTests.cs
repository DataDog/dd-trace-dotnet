// <copyright file="RabbitMQTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Trait("RequiresDockerDependency", "true")]
    public class RabbitMQTests : TestHelper
    {
        private const string ExpectedServiceName = "Samples.RabbitMQ-rabbitmq";

        public RabbitMQTests(ITestOutputHelper output)
            : base("RabbitMQ", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.RabbitMQ), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public void SubmitsTraces(string packageVersion)
        {
#if NET6_0_OR_GREATER
            if (packageVersion?.StartsWith("3.") == true)
            {
                // Versions 3.* of RabbitMQ.Client aren't compatible with .NET 6
                // https://github.com/dotnet/runtime/issues/61167
                return;
            }
#endif

            var expectedSpanCount = 52;

            int basicPublishCount = 0;
            int basicGetCount = 0;
            int basicDeliverCount = 0;
            int exchangeDeclareCount = 0;
            int queueDeclareCount = 0;
            int queueBindCount = 0;
            var distributedParentSpans = new Dictionary<ulong, int>();

            int emptyBasicGetCount = 0;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                var spans = agent.WaitForSpans(expectedSpanCount); // Do not filter on operation name because they will vary depending on instrumented method
                Assert.True(spans.Count >= expectedSpanCount, $"Expecting at least {expectedSpanCount} spans, only received {spans.Count}");

                var rabbitmqSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));
                var manualSpans = spans.Where(span => !string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));

                foreach (var span in rabbitmqSpans)
                {
                    var result = span.IsRabbitMQ();
                    Assert.True(result.Success, result.ToString());

                    Assert.False(span.Tags?.ContainsKey(Tags.Version), "External service span should not have service version tag.");

                    var command = span.Tags[Tags.AmqpCommand];

                    if (command.StartsWith("basic.", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(command, "basic.publish", StringComparison.OrdinalIgnoreCase))
                        {
                            basicPublishCount++;
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
                            basicGetCount++;

                            // Successful responses will have the "message.size" tag
                            // Empty responses will not
                            if (span.Tags.TryGetValue("message.size", out string messageSize))
                            {
                                Assert.NotNull(span.ParentId);
                                Assert.True(int.TryParse(messageSize, out _));

                                // Add the parent span ID to a dictionary so we can later assert 1:1 mappings
                                if (distributedParentSpans.TryGetValue(span.ParentId.Value, out int count))
                                {
                                    distributedParentSpans[span.ParentId.Value] = count + 1;
                                }
                                else
                                {
                                    distributedParentSpans[span.ParentId.Value] = 1;
                                }
                            }
                            else
                            {
                                emptyBasicGetCount++;
                            }

                            Assert.Equal(SpanKinds.Consumer, span.Tags[Tags.SpanKind]);
                            Assert.NotNull(span.Tags[Tags.AmqpQueue]);

                            // Enforce that the resource name has the following structure: "basic.get [<generated>|{actual queueName}]"
                            string regexPattern = @"basic\.get (?<queueName>\S*)";
                            var match = Regex.Match(span.Resource, regexPattern);
                            Assert.True(match.Success);

                            var queueName = match.Groups["queueName"].Value;
                            Assert.True(string.Equals(queueName, "<generated>") || string.Equals(queueName, span.Tags[Tags.AmqpQueue]));
                        }
                        else if (string.Equals(command, "basic.deliver", StringComparison.OrdinalIgnoreCase))
                        {
                            basicDeliverCount++;
                            Assert.NotNull(span.ParentId);

                            // Add the parent span ID to a dictionary so we can later assert 1:1 mappings
                            if (distributedParentSpans.TryGetValue(span.ParentId.Value, out int count))
                            {
                                distributedParentSpans[span.ParentId.Value] = count + 1;
                            }
                            else
                            {
                                distributedParentSpans[span.ParentId.Value] = 1;
                            }

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
                            exchangeDeclareCount++;
                            Assert.NotNull(span.Tags[Tags.AmqpExchange]);
                        }
                        else if (string.Equals(command, "queue.declare", StringComparison.OrdinalIgnoreCase))
                        {
                            queueDeclareCount++;
                            Assert.NotNull(span.Tags[Tags.AmqpQueue]);
                        }
                        else if (string.Equals(command, "queue.bind", StringComparison.OrdinalIgnoreCase))
                        {
                            queueBindCount++;
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

                foreach (var span in manualSpans)
                {
                    Assert.Equal("Samples.RabbitMQ", span.Service);
                    Assert.Equal("1.0.0", span.Tags[Tags.Version]);
                    Assert.True(rabbitmqSpans.Count(s => s.TraceId == span.TraceId) > 0);
                }
            }

            // Assert that all empty get results are expected
            Assert.Equal(4, emptyBasicGetCount);

            // Assert that each span that started a distributed trace (basic.publish)
            // has only one child span (basic.deliver or basic.get)
            Assert.All(distributedParentSpans, kvp => Assert.Equal(1, kvp.Value));

            Assert.Equal(10, basicPublishCount);
            Assert.Equal(8, basicGetCount);
            Assert.Equal(6, basicDeliverCount);

            Assert.Equal(2, exchangeDeclareCount);
            Assert.Equal(2, queueBindCount);
            Assert.Equal(8, queueDeclareCount);
            telemetry.AssertIntegrationEnabled(IntegrationId.RabbitMQ);
        }
    }
}
