// <copyright file="RabbitMQTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using FluentAssertions.Execution;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    [Trait("RequiresDockerDependency", "true")]
    public class RabbitMQTests : TracingIntegrationTest
    {
        private const string ExpectedServiceName = "Samples.RabbitMQ-rabbitmq";
        private static readonly Regex GeneratedQueueRegex = new(@"(amqp\.queue\:) amq\.gen\-.*,");
        private static readonly Regex GeneratedRoutingKeyRegex = new(@"(amqp\.routing_key\:) amq\.gen\-.*,");

        public RabbitMQTests(ITestOutputHelper output)
            : base("RabbitMQ", output)
        {
            SetServiceVersion("1.0.0");
        }

        public override Result ValidateIntegrationSpan(MockSpan span) => span.IsRabbitMQ();

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.RabbitMQ), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitsTraces(string packageVersion)
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

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                using var assertionScope = new AssertionScope();
                var spans = agent.WaitForSpans(expectedSpanCount); // Do not filter on operation name because they will vary depending on instrumented method

                var rabbitmqSpans = spans.Where(span => string.Equals(span.Service, ExpectedServiceName, StringComparison.OrdinalIgnoreCase));

                ValidateIntegrationSpans(rabbitmqSpans, expectedServiceName: "Samples.RabbitMQ-rabbitmq");
                var settings = VerifyHelper.GetSpanVerifierSettings();

                // We generate a new queue name for the "default" queue with each run
                settings.AddScrubber(QueueScrubber.ReplaceRabbitMqQueues);

                var filename = $"{nameof(RabbitMQTests)}.{GetPackageVersionSuffix(packageVersion)}";
                await VerifyHelper.VerifySpans(spans, settings)
                                  .UseFileName(filename);
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.RabbitMQ);
        }

        private string GetPackageVersionSuffix(string packageVersion)
            => packageVersion switch
            {
                null or "" => "6_x", // the default version in the csproj
                _ when new Version(packageVersion) >= new Version("6.0.0") => "6_x",
                _ when new Version(packageVersion) >= new Version("5.0.0") => "5_x",
                _ => "3_x",
            };

        private class QueueScrubber
        {
            private static readonly Regex Regex = new(@"amq\.gen\-.*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static void ReplaceRabbitMqQueues(StringBuilder builder)
            {
                if (!TryReplaceRabbitMqQueue(builder.ToString(), out var result))
                {
                    return;
                }

                builder.Clear();
                builder.Append(result);
            }

            private static bool TryReplaceRabbitMqQueue(string value, out string result)
            {
                var queues = Regex.Matches(value);
                var index = 0;
                if (queues.Count > 0)
                {
                    result = value;
                    foreach (Match queueMatch in queues)
                    {
                        var queue = queueMatch!.Value;
                        index++;
                        var convertedQueue = $"AmqQueue_{index}";

                        result = result.Replace(queue, convertedQueue);
                    }

                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
