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
        public RabbitMQTests(ITestOutputHelper output)
            : base("RabbitMQ", output)
        {
            SetServiceVersion("1.0.0");
        }

        public static IEnumerable<object[]> GetEnabledConfig()
            => from packageVersionArray in PackageVersions.RabbitMQ
               from metadataSchemaVersion in new[] { "v0", "v1" }
               select new[] { packageVersionArray[0], metadataSchemaVersion };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Tags["span.kind"] switch
            {
                SpanKinds.Consumer => span.IsRabbitMQInbound(metadataSchemaVersion),
                SpanKinds.Producer => span.IsRabbitMQOutbound(metadataSchemaVersion),
                SpanKinds.Client => span.IsRabbitMQAdmin(metadataSchemaVersion),
                _ => throw new ArgumentException($"span.Tags[\"span.kind\"] is not a supported value for the RabbitMQ integration: {span.Tags["span.kind"]}", nameof(span)),
            };

        [SkippableTheory]
        [MemberData(nameof(GetEnabledConfig))]
        [Trait("Category", "EndToEnd")]
        public async Task SubmitTraces(string packageVersion, string metadataSchemaVersion)
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

            SetEnvironmentVariable("DD_TRACE_SPAN_ATTRIBUTE_SCHEMA", metadataSchemaVersion);
            var isExternalSpan = metadataSchemaVersion == "v0";
            var clientSpanServiceName = isExternalSpan ? $"{EnvironmentHelper.FullSampleName}-rabbitmq" : EnvironmentHelper.FullSampleName;

            using var telemetry = this.ConfigureTelemetry();
            using (var agent = EnvironmentHelper.GetMockAgent())
            using (await RunSampleAndWaitForExit(agent, arguments: $"{TestPrefix}", packageVersion: packageVersion))
            {
                using var assertionScope = new AssertionScope();
                var spans = agent.WaitForSpans(expectedSpanCount); // Do not filter on operation name because they will vary depending on instrumented method

                var rabbitmqSpans = spans.Where(span => string.Equals(span.GetTag("component"), "RabbitMQ", StringComparison.OrdinalIgnoreCase));

                ValidateIntegrationSpans(rabbitmqSpans, metadataSchemaVersion, expectedServiceName: clientSpanServiceName, isExternalSpan);
                var settings = VerifyHelper.GetSpanVerifierSettings();

                // We generate a new queue name for the "default" queue with each run
                settings.AddScrubber(QueueScrubber.ReplaceRabbitMqQueues);
                settings.AddSimpleScrubber("out.host: localhost", "out.host: rabbitmq");
                settings.AddSimpleScrubber("out.host: rabbitmq_arm64", "out.host: rabbitmq");
                settings.AddSimpleScrubber("peer.service: localhost", "peer.service: rabbitmq");
                settings.AddSimpleScrubber("peer.service: rabbitmq_arm64", "peer.service: rabbitmq");

                var filename = $"{nameof(RabbitMQTests)}.{GetPackageVersionSuffix(packageVersion)}";

                // Default sorting isn't very reliable, so use our own (adds in name and resource)
                await VerifyHelper.VerifySpans(
                                       spans,
                                       settings,
                                       s => s
                                           .OrderBy(x => VerifyHelper.GetRootSpanResourceName(x, spans))
                                           .ThenBy(x => VerifyHelper.GetSpanDepth(x, spans))
                                           .ThenBy(x => x.Name)
                                           .ThenBy(x => x.Resource)
                                           .ThenBy(x => x.Start)
                                           .ThenBy(x => x.Duration))
                                  .UseFileName(filename + $".Schema{metadataSchemaVersion.ToUpper()}")
                                  .DisableRequireUniquePrefix();
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
