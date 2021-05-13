using System.Text;
using Confluent.Kafka;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Kafka
{
    public class CachedMessageHeadersHelperTests
    {
        [Fact]
        public void CanCreateAndAssignMessageHeaders()
        {
            var headersProxy = CachedMessageHeadersHelper<TopicPartition>.CreateHeaders();

            headersProxy.Should().NotBeNull();
            headersProxy.Add("key", Encoding.UTF8.GetBytes("value"));

            headersProxy.TryGetLastBytes("key", out var headerValue).Should().BeTrue();
            Encoding.UTF8.GetString(headerValue).Should().Be("value");
        }
    }
}
