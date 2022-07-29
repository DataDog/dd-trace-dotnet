// <copyright file="KafkaTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(KafkaTestsCollection))]
    [Trait("RequiresDockerDependency", "true")]
    public class KafkaTests : TestHelper
    {
        private const int ExpectedSuccessProducerWithHandlerSpans = 20;
        private const int ExpectedSuccessProducerWithoutHandlerSpans = 10;
        private const int ExpectedSuccessProducerSpans = ExpectedSuccessProducerWithHandlerSpans + ExpectedSuccessProducerWithoutHandlerSpans;
        private const int ExpectedTombstoneProducerWithHandlerSpans = 20;
        private const int ExpectedTombstoneProducerWithoutHandlerSpans = 10;
        private const int ExpectedTombstoneProducerSpans = ExpectedTombstoneProducerWithHandlerSpans + ExpectedTombstoneProducerWithoutHandlerSpans;
        private const int ExpectedErrorProducerSpans = 2; // When no delivery handler, error can't be caught, so we don't test that case
        private const int ExpectedConsumerSpans = ExpectedSuccessProducerSpans + ExpectedTombstoneProducerSpans;
        private const int TotalExpectedSpanCount = ExpectedConsumerSpans
                                                 + ExpectedSuccessProducerSpans
                                                 + ExpectedTombstoneProducerSpans
                                                 + ExpectedErrorProducerSpans;

        private const string ErrorProducerResourceName = "Produce Topic INVALID-TOPIC";

        public KafkaTests(ITestOutputHelper output)
            : base("Kafka", output)
        {
            SetServiceVersion("1.0.0");
        }

        [SkippableTheory]
        [MemberData(nameof(PackageVersions.Kafka), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            var topic = $"sample-topic-{TestPrefix}-{packageVersion}".Replace('.', '-');

            using var telemetry = this.ConfigureTelemetry();
            using var agent = EnvironmentHelper.GetMockAgent();
            using var processResult = RunSampleAndWaitForExit(agent, arguments: topic, packageVersion: packageVersion);

            var allSpans = agent.WaitForSpans(TotalExpectedSpanCount, timeoutInMilliseconds: 10_000);
            using var assertionScope = new AssertionScope();
            // We use HaveCountGreaterOrEqualTo because _both_ consumers may handle the message
            // Due to manual/autocommit behaviour
            allSpans.Should().HaveCountGreaterOrEqualTo(TotalExpectedSpanCount);

            foreach (var span in allSpans)
            {
                var result = span.IsKafka();
                Assert.True(result.Success, result.ToString());
            }

            var allProducerSpans = allSpans.Where(x => x.Name == "kafka.produce").ToList();
            var successfulProducerSpans = allProducerSpans.Where(x => x.Error == 0).ToList();
            var errorProducerSpans = allProducerSpans.Where(x => x.Error > 0).ToList();

            var allConsumerSpans = allSpans.Where(x => x.Name == "kafka.consume").ToList();
            var successfulConsumerSpans = allConsumerSpans.Where(x => x.Error == 0).ToList();
            var errorConsumerSpans = allConsumerSpans.Where(x => x.Error > 0).ToList();

            VerifyProducerSpanProperties(successfulProducerSpans, GetSuccessfulResourceName("Produce", topic), ExpectedSuccessProducerSpans + ExpectedTombstoneProducerSpans);
            VerifyProducerSpanProperties(errorProducerSpans, ErrorProducerResourceName, ExpectedErrorProducerSpans);

            // Only successful spans with a delivery handler will have an offset
            successfulProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.KafkaOffset))
               .Select(span => span.Tags[Tags.KafkaOffset])
               .Should()
               .OnlyContain(tag => Regex.IsMatch(tag, @"^[0-9]+$"))
               .And.HaveCount(ExpectedSuccessProducerWithHandlerSpans + ExpectedTombstoneProducerWithHandlerSpans);

            // Only successful spans with a delivery handler will have a partition
            // Confirm partition is displayed correctly [0], [1]
            // https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/Partition.cs#L217-L224
            successfulProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.KafkaPartition))
               .Select(span => span.Tags[Tags.KafkaPartition])
               .Should()
               .OnlyContain(tag => Regex.IsMatch(tag, @"^\[[0-9]+\]$"))
               .And.HaveCount(ExpectedSuccessProducerWithHandlerSpans + ExpectedTombstoneProducerWithHandlerSpans);

            allProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.KafkaTombstone))
               .Select(span => span.Tags[Tags.KafkaTombstone])
               .Should()
               .HaveCount(ExpectedTombstoneProducerSpans)
               .And.OnlyContain(tag => tag == "true");

            // verify have error
            errorProducerSpans.Should().OnlyContain(x => x.Tags.ContainsKey(Tags.ErrorType))
                              .And.ContainSingle(x => x.Tags[Tags.ErrorType] == "Confluent.Kafka.ProduceException`2[System.String,System.String]") // created by async handler
                              .And.ContainSingle(x => x.Tags[Tags.ErrorType] == "System.Exception"); // created by sync callback handler

            var producerSpanIds = successfulProducerSpans
                                 .Select(x => x.SpanId)
                                 .Should()
                                 .OnlyHaveUniqueItems()
                                 .And.Subject.ToImmutableHashSet();

            VerifyConsumerSpanProperties(successfulConsumerSpans, GetSuccessfulResourceName("Consume", topic), ExpectedConsumerSpans);

            // every consumer span should be a child of a producer span.
            successfulConsumerSpans
               .Should()
               .OnlyContain(span => span.ParentId.HasValue)
               .And.OnlyContain(span => producerSpanIds.Contains(span.ParentId.Value));

            // HaveCountGreaterOrEqualTo because same message may be consumed by both
            successfulConsumerSpans
               .Where(span => span.Tags.ContainsKey(Tags.KafkaTombstone))
               .Select(span => span.Tags[Tags.KafkaTombstone])
               .Should()
               .HaveCountGreaterOrEqualTo(ExpectedTombstoneProducerSpans)
               .And.OnlyContain(tag => tag == "true");

            // Error spans are created in 1.5.3 when the broker doesn't exist yet
            // Other package versions don't error, so won't create a span,
            // so no fixed number requirement
            if (errorConsumerSpans.Count > 0)
            {
                errorConsumerSpans
                   .Should()
                   .OnlyContain(x => x.Tags.ContainsKey(Tags.ErrorType))
                   .And.OnlyContain(x => x.Tags[Tags.ErrorMsg].Contains("Broker: Unknown topic or partition"))
                   .And.OnlyContain(x => x.Tags[Tags.ErrorType] == "Confluent.Kafka.ConsumeException");
            }

            telemetry.AssertIntegrationEnabled(IntegrationId.Kafka);
        }

        private void VerifyProducerSpanProperties(List<MockSpan> producerSpans, string resourceName, int expectedCount)
        {
            producerSpans.Should()
                         .HaveCount(expectedCount)
                         .And.OnlyContain(x => x.Service == "Samples.Kafka-kafka")
                         .And.OnlyContain(x => x.Resource == resourceName)
                         .And.OnlyContain(x => x.Metrics.ContainsKey(Tags.Measured) && x.Metrics[Tags.Measured] == 1.0);
        }

        private void VerifyConsumerSpanProperties(List<MockSpan> consumerSpans, string resourceName, int expectedCount)
        {
            // HaveCountGreaterOrEqualTo because same message may be consumed by both
            consumerSpans.Should()
                         .HaveCountGreaterOrEqualTo(expectedCount)
                         .And.OnlyContain(x => x.Service == "Samples.Kafka-kafka")
                         .And.OnlyContain(x => x.Resource == resourceName)
                         .And.OnlyContain(x => x.Metrics.ContainsKey(Tags.Measured) && x.Metrics[Tags.Measured] == 1.0)
                         .And.OnlyContain(x => x.Metrics.ContainsKey(Metrics.MessageQueueTimeMs))
                         .And.OnlyContain(x => x.Tags.ContainsKey(Tags.KafkaOffset) && Regex.IsMatch(x.Tags[Tags.KafkaOffset], @"^[0-9]+$"))
                         .And.OnlyContain(x => x.Tags.ContainsKey(Tags.KafkaPartition) && Regex.IsMatch(x.Tags[Tags.KafkaPartition], @"^\[[0-9]+\]$"));
        }

        private string GetSuccessfulResourceName(string type, string topic)
        {
            return $"{type} Topic {topic}";
        }

        [CollectionDefinition(nameof(KafkaTestsCollection), DisableParallelization = true)]
        public class KafkaTestsCollection
        {
        }
    }
}
