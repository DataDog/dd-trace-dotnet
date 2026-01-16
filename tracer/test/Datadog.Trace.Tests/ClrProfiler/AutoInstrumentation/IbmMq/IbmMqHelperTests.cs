// <copyright file="IbmMqHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.ClrProfiler.AutoInstrumentation.IbmMq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.IbmMq
{
    public class IbmMqHelperTests
    {
        [Theory]
        [InlineData("queue://my_queue", "my_queue")]
        [InlineData("queue://DEV.QUEUE.1", "DEV.QUEUE.1")]
        [InlineData("queue://my_ibmmq.queue.1", "my_ibmmq.queue.1")]
        [InlineData("QUEUE://MY_QUEUE", "MY_QUEUE")] // case insensitive
        [InlineData("Queue://Mixed.Case.Queue", "Mixed.Case.Queue")] // case insensitive
        public void SanitizeQueueName_RemovesQueueUriPrefix(string input, string expected)
        {
            var result = IbmMqHelper.SanitizeQueueName(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("topic://my_topic", "my_topic")]
        [InlineData("topic://DEV.TOPIC.1", "DEV.TOPIC.1")]
        [InlineData("TOPIC://MY_TOPIC", "MY_TOPIC")] // case insensitive
        [InlineData("Topic://Mixed.Case.Topic", "Mixed.Case.Topic")] // case insensitive
        public void SanitizeQueueName_RemovesTopicUriPrefix(string input, string expected)
        {
            var result = IbmMqHelper.SanitizeQueueName(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("DEV.QUEUE.1", "DEV.QUEUE.1")]
        [InlineData("my_queue", "my_queue")]
        [InlineData("MY.QUEUE.NAME", "MY.QUEUE.NAME")]
        [InlineData("simple", "simple")]
        public void SanitizeQueueName_PreservesNamesWithoutPrefix(string input, string expected)
        {
            var result = IbmMqHelper.SanitizeQueueName(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        public void SanitizeQueueName_HandlesNullAndEmptyStrings(string? input, string expected)
        {
            var result = IbmMqHelper.SanitizeQueueName(input);

            result.Should().Be(expected);
        }

        [Theory]
        [InlineData("http://not_a_queue", "http://not_a_queue")]
        [InlineData("https://not_a_queue", "https://not_a_queue")]
        [InlineData("amqp://not_a_queue", "amqp://not_a_queue")]
        [InlineData("queues://not_a_queue", "queues://not_a_queue")] // note: "queues" not "queue"
        [InlineData("queue:/missing_slash", "queue:/missing_slash")] // malformed - single slash
        public void SanitizeQueueName_PreservesOtherUriSchemes(string input, string expected)
        {
            var result = IbmMqHelper.SanitizeQueueName(input);

            result.Should().Be(expected);
        }

        [Fact]
        public void SanitizeQueueName_HandlesQueueNameWithSpecialCharacters()
        {
            var result = IbmMqHelper.SanitizeQueueName("queue://MY.QUEUE_NAME-123");

            result.Should().Be("MY.QUEUE_NAME-123");
        }

        [Fact]
        public void SanitizeQueueName_HandlesEmptyQueueNameAfterPrefix()
        {
            var result = IbmMqHelper.SanitizeQueueName("queue://");

            result.Should().Be(string.Empty);
        }
    }
}
