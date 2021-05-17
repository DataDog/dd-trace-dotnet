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
        private static readonly Type MessageType = typeof(Message<,>);
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

            // Now use LoadFile to load a second instance and re-run the tests
            var loadFileAssembly = Assembly.LoadFile(TopicPartitionType.Assembly.Location);
            var loadHeadersDetails = CreateGenericCreateHeadersMethod(loadFileAssembly);
            var loadFileHeadersProxy = (IHeaders)loadHeadersDetails.CreateHeaders.Invoke(null, null);
            AssertHeadersProxy(loadFileHeadersProxy);
            messageProxy = loadHeadersDetails.Message;

#if NETCOREAPP3_1 || NET5_0
            var alc = new System.Runtime.Loader.AssemblyLoadContext($"NewAssemblyLoadContext");
            var loadContextAssembly = alc.LoadFromAssemblyPath(TopicPartitionType.Assembly.Location);
            var loadContextDetails = CreateGenericCreateHeadersMethod(loadContextAssembly);

            var loadContextHeadersProxy = (IHeaders)loadContextDetails.CreateHeaders.Invoke(null, null);
            AssertHeadersProxy(loadContextHeadersProxy);
            messageProxy = loadContextDetails.Message;
#endif
        }

        private static void AssertHeadersProxy(IHeaders headersProxy)
        {
            headersProxy.Should().NotBeNull();
            headersProxy.Add("key", Encoding.UTF8.GetBytes("value"));

            headersProxy.TryGetLastBytes("key", out var headerValue).Should().BeTrue();
            Encoding.UTF8.GetString(headerValue).Should().Be("value");
        }

        private static (MethodInfo CreateHeaders, IMessage Message) CreateGenericCreateHeadersMethod(Assembly assembly)
        {
            var topicPartitionType = assembly.GetType(TopicPartitionType.FullName);
            topicPartitionType.Should().NotBeNull();
            var helperType = CachedMessageHeadersHelperType.MakeGenericType(topicPartitionType);
            helperType.Should().NotBeNull();
            var createHeadersMethod = helperType.GetMethod(
                nameof(CachedMessageHeadersHelper<TopicPartition>.CreateHeaders),
                BindingFlags.Public | BindingFlags.Static);
            createHeadersMethod.Should().NotBeNull();

            var messageType = assembly.GetType(MessageType.FullName);
            messageType.Should().NotBeNull();
            var genericMessage = messageType.MakeGenericType(typeof(string), typeof(string));

            var message = Activator.CreateInstance(genericMessage);
            var proxy = message.DuckCast<IMessage>();

            return (createHeadersMethod, proxy);
        }
    }
}
