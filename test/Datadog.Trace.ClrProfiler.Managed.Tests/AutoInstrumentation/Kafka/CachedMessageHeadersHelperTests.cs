using System;
using System.Reflection;
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
        private static readonly Type TopicPartitionType = typeof(TopicPartition);
        private static readonly Type CachedMessageHeadersHelperType = typeof(CachedMessageHeadersHelper<>);

        [Fact]
        public void CanCreateAndAssignMessageHeaders()
        {
            var message = new Message<string, string>();
            var messageProxy = message.DuckCast<IMessage>();

            // Call the CachedMessageHeadersHelper using the compile-time assembly
            var headersProxy = CachedMessageHeadersHelper<TopicPartition>.CreateHeaders();
            AssertHeadersProxy(headersProxy);
            messageProxy.Headers = headersProxy;
            message.Headers.Should().NotBeNull();

            // Now use LoadFile to load a second instance and re-run the tests
            message.Headers = null;
            var loadFileAssembly = Assembly.LoadFile(TopicPartitionType.Assembly.Location);
            var loadFileCreateHeadersMethod = CreateGenericCreateHeadersMethod(loadFileAssembly);

            var loadFileHeadersProxy = (IHeaders)loadFileCreateHeadersMethod.Invoke(null, null);
            AssertHeadersProxy(loadFileHeadersProxy);
            messageProxy.Headers = loadFileHeadersProxy;
            message.Headers.Should().NotBeNull();

#if NETCOREAPP3_1 || NET5_0
            message.Headers = null;
            var alc = new System.Runtime.Loader.AssemblyLoadContext($"NewAssemblyLoadContext");
            var loadContextAssembly = alc.LoadFromAssemblyPath(TopicPartitionType.Assembly.Location);
            var loadContextCreateHeadersMethod = CreateGenericCreateHeadersMethod(loadContextAssembly);

            var loadContextHeadersProxy = (IHeaders)loadContextCreateHeadersMethod.Invoke(null, null);
            AssertHeadersProxy(loadContextHeadersProxy);
            messageProxy.Headers = loadContextHeadersProxy;
            message.Headers.Should().NotBeNull();
#endif
        }

        private static void AssertHeadersProxy(IHeaders headersProxy)
        {
            headersProxy.Should().NotBeNull();
            headersProxy.Add("key", Encoding.UTF8.GetBytes("value"));

            headersProxy.TryGetLastBytes("key", out var headerValue).Should().BeTrue();
            Encoding.UTF8.GetString(headerValue).Should().Be("value");
        }

        private static MethodInfo CreateGenericCreateHeadersMethod(Assembly loadFileAssembly)
        {
            var topicPartitionType = loadFileAssembly.GetType(TopicPartitionType.FullName);
            topicPartitionType.Should().NotBeNull();
            var helperType = CachedMessageHeadersHelperType.MakeGenericType(topicPartitionType);
            helperType.Should().NotBeNull();
            var createHeadersMethod = helperType.GetMethod(
                nameof(CachedMessageHeadersHelper<TopicPartition>.CreateHeaders),
                BindingFlags.Public | BindingFlags.Static);
            createHeadersMethod.Should().NotBeNull();
            return createHeadersMethod;
        }
    }
}
