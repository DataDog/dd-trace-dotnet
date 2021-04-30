using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection(nameof(KafkaTestsCollection))]
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
            SetCallTargetSettings(enableCallTarget: true);
            SetEnvironmentVariable("DD_TRACE_DEBUG", "1");
        }

        [Theory]
        [MemberData(nameof(PackageVersions.Kafka), MemberType = typeof(PackageVersions))]
        [Trait("Category", "EndToEnd")]
        [Trait("Category", "ArmUnsupported")]
        public void SubmitsTraces(string packageVersion)
        {
            var topic = $"sample-topic-{TestPrefix}-{packageVersion}".Replace('.', '-');
            var agentPort = TcpPortProvider.GetOpenPort();

            using var agent = new MockTracerAgent(agentPort);
            using var processResult = RunSampleAndWaitForExit(agent.Port, arguments: topic, packageVersion: packageVersion);

            processResult.ExitCode.Should().BeGreaterOrEqualTo(0);

            var allSpans = agent.WaitForSpans(TotalExpectedSpanCount, timeoutInMilliseconds: 10_000);
            using var assertionScope = new AssertionScope();
            allSpans.Should().HaveCount(TotalExpectedSpanCount);

            var allProducerSpans = allSpans.Where(x => x.Name == "kafka.produce").ToList();
            var successfulProducerSpans = allProducerSpans.Where(x => x.Error == 0).ToList();
            var errorProducerSpans = allProducerSpans.Where(x => x.Error > 0).ToList();
            var allConsumerSpans = allSpans.Where(x => x.Name == "kafka.consume").ToList();

            VerifyProducerSpanProperties(successfulProducerSpans, GetSuccessfulResourceName("Produce", topic), ExpectedSuccessProducerSpans + ExpectedTombstoneProducerSpans);
            VerifyProducerSpanProperties(errorProducerSpans, ErrorProducerResourceName, ExpectedErrorProducerSpans);

            // Only successful spans with a delivery handler will have an offset
            successfulProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.Offset))
               .Select(span => span.Tags[Tags.Offset])
               .Should()
               .OnlyContain(tag => Regex.IsMatch(tag, @"^[0-9]+$"))
               .And.HaveCount(ExpectedSuccessProducerWithHandlerSpans + ExpectedTombstoneProducerWithHandlerSpans);

            // Only successful spans with a delivery handler will have a partition
            // Confirm partition is displayed correctly [0], [1]
            // https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/Partition.cs#L217-L224
            successfulProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.Partition))
               .Select(span => span.Tags[Tags.Partition])
               .Should()
               .OnlyContain(tag => Regex.IsMatch(tag, @"^\[[0-9]+\]$"))
               .And.HaveCount(ExpectedSuccessProducerWithHandlerSpans + ExpectedTombstoneProducerWithHandlerSpans);

            allProducerSpans
               .Where(span => span.Tags.ContainsKey(Tags.Tombstone))
               .Select(span => span.Tags[Tags.Tombstone])
               .Should()
               .HaveCount(ExpectedTombstoneProducerSpans)
               .And.OnlyContain(tag => tag == "true");

            // verify have error
            errorProducerSpans.Should().OnlyContain(x => x.Tags.ContainsKey(Tags.ErrorType))
                              .And.ContainSingle(x => x.Tags[Tags.ErrorType] == "Confluent.Kafka.ProduceException`2[System.String,System.String]") // created by async handler
                              .And.ContainSingle(x => x.Tags[Tags.ErrorType] == "System.Exception"); // created by sync callback handler

            VerifyConsumerSpanProperties(allConsumerSpans, GetSuccessfulResourceName("Consume", topic), ExpectedConsumerSpans);

            // every consumer span should be a child of a producer span.
            var producerSpanIds = new HashSet<ulong>(successfulProducerSpans.Select(x => x.SpanId));
            producerSpanIds.Should().HaveCount(successfulProducerSpans.Count);
            allConsumerSpans
               .Should()
               .OnlyContain(span => span.ParentId.HasValue)
               .And.OnlyContain(span => producerSpanIds.Contains(span.ParentId.Value));

            allConsumerSpans
               .Where(span => span.Tags.ContainsKey(Tags.Tombstone))
               .Select(span => span.Tags[Tags.Tombstone])
               .Should()
               .HaveCount(ExpectedTombstoneProducerSpans)
               .And.OnlyContain(tag => tag == "true");
        }

        private void VerifyProducerSpanProperties(List<MockTracerAgent.Span> producerSpans, string resourceName, int expectedCount)
        {
            producerSpans.Should()
                         .HaveCount(expectedCount)
                         .And.OnlyContain(x => x.Service == "Samples.Kafka-kafka")
                         .And.OnlyContain(x => x.Resource == resourceName)
                         .And.OnlyContain(x => x.Metrics.ContainsKey(Tags.Measured) && x.Metrics[Tags.Measured] == 1.0);
        }

        private void VerifyConsumerSpanProperties(List<MockTracerAgent.Span> consumerSpans, string resourceName, int expectedCount)
        {
            consumerSpans.Should()
                         .HaveCount(expectedCount)
                         .And.OnlyContain(x => x.Service == "Samples.Kafka-kafka")
                         .And.OnlyContain(x => x.Resource == resourceName)
                         .And.OnlyContain(x => x.Metrics.ContainsKey(Tags.Measured) && x.Metrics[Tags.Measured] == 1.0)
                         .And.OnlyContain(x => x.Tags.ContainsKey(Tags.RecordQueueTimeMs))
                         .And.OnlyContain(x => x.Tags.ContainsKey(Tags.Offset) && Regex.IsMatch(x.Tags[Tags.Offset], @"^[0-9]+$"))
                         .And.OnlyContain(x => x.Tags.ContainsKey(Tags.Partition) && Regex.IsMatch(x.Tags[Tags.Partition], @"^\[[0-9]+\]$"));
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
