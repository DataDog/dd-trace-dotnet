using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

#if RABBITMQ_7_0
using IRabbitChannel = RabbitMQ.Client.IChannel;
using IRabbitConsumer = RabbitMQ.Client.IAsyncBasicConsumer;
using RabbitProperties = RabbitMQ.Client.BasicProperties;
#else
using IRabbitChannel = RabbitMQ.Client.IModel;
using IRabbitConsumer = RabbitMQ.Client.IBasicConsumer;
using RabbitProperties = RabbitMQ.Client.IBasicProperties;
#endif

namespace Samples.RabbitMQ
{
    public static class Program
    {
        static long _messageCount = 0;
        static AutoResetEvent _sendFinished = new AutoResetEvent(false);

        private static readonly string exchangeName = "test-exchange-name";
        private static readonly string routingKey = "test-routing-key";
        private static readonly string queueName = "test-queue-name";
        private static readonly string customHeaderName = "x-custom-header";

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        }

        public static async Task Main(string[] args)
        {
            // connecting takes 2 to 3 seconds, we re-use the connection to save time
            var factory = new ConnectionFactory() { HostName = Host() };
#if RABBITMQ_5_0 && !RABBITMQ_7_0
            factory.DispatchConsumersAsync = true;
#endif
            using (var asyncConnection = await Helper.CreateConnectionAsync(factory))
            {
                // Test a derived type for the async consumer from the library
                await RunProducersAndConsumers(asyncConnection, useQueue: true, ConsumerType.InternalAsyncDerived, isAsyncConsumer: true);

                // Test a custom type that implements the async consumer inteface, using explicit interface implementation
                await RunProducersAndConsumers(asyncConnection, useQueue: true, ConsumerType.ExternalExplicit, isAsyncConsumer: true);
            }

            factory = new ConnectionFactory() { HostName = Host() };
            using (var syncConnection = await Helper.CreateConnectionAsync(factory))
            {
                // Test a derived type for the sync consumer from the library
                await RunProducersAndConsumers(syncConnection, useQueue: false, ConsumerType.InternalSyncDerived, isAsyncConsumer: false);
                // Test a custom type that implements the sync consumer interface, using implicit interface implementation
                await RunProducersAndConsumers(syncConnection, useQueue: true, ConsumerType.ExternalImplicit, isAsyncConsumer: false);
            }
        }

        private static async Task RunProducersAndConsumers(IConnection connection, bool useQueue, ConsumerType consumerType, bool isAsyncConsumer)
        {
            using (var channel = await Helper.CreateChannelAsync(connection))
            {
                await PublishAndGet(channel, consumerType.ToString(), useDefaultQueue: false);
                await PublishAndGet(channel, consumerType.ToString(), useDefaultQueue: true);


                var sendTask = Task.Run(() => Send(connection, consumerType.ToString()));
                var receiveTask = Task.Run(() => Receive(connection, useQueue, consumerType, isAsyncConsumer));

                var allTasks = Task.WhenAll(sendTask, receiveTask);

                var completed = await Task.WhenAny(allTasks, Task.Delay(TimeSpan.FromMinutes(3))); // Intentionally very big
                if (completed != allTasks)
                {
                    throw new TimeoutException("Timeout waiting for the Send and receive tasks to complete");
                }

                _sendFinished.Reset();
            }
        }

        private static async Task PublishAndGet(IRabbitChannel channel, string consumerType, bool useDefaultQueue)
        {
            string messagePrefix = $"Program.PublishAndGetDefault({consumerType}, useDefaultQueue: {useDefaultQueue})";

            // Configure and send to RabbitMQ queue
            string publishExchangeName;
            string publishQueueName;
            string publishRoutingKey;
            string messageId = Guid.NewGuid().ToString();
            string headerValue = Guid.NewGuid().ToString();

            using (SampleHelpers.CreateScope(messagePrefix))
            {
                if (useDefaultQueue)
                {
                    publishExchangeName = "";
                    publishQueueName = (await Helper.QueueDeclareAsync(channel)).QueueName;
                    publishRoutingKey = publishQueueName;
                }
                else
                {
                    publishExchangeName = exchangeName;
                    publishQueueName = queueName;
                    publishRoutingKey = routingKey;

                    await Helper.ExchangeDeclareAsync(channel, publishExchangeName, "direct");

                    await Helper.QueueDeclareAsync(channel, queue: publishQueueName);

                    await Helper.QueueBindAsync(channel, publishQueueName, publishExchangeName, publishRoutingKey);

                }

                // Ensure there are no more messages in this queue
                await Helper.QueuePurgeAsync(channel, publishQueueName);

                // Test an empty BasicGetResult
                await Helper.BasicGetAsync(channel, publishQueueName);

                // Setup basic properties to verify instrumentation preserves properties and headers.
                var properties = Helper.CreateBasicProperties(channel);
                properties.MessageId = messageId;
                properties.Headers = new Dictionary<string, object>
                    {
                        { customHeaderName, headerValue }
                    };

                // Send message to the default exchange and use new queue as the routingKey
                string message = $"{messagePrefix} - Message";
                var body = Encoding.UTF8.GetBytes(message);

#if RABBITMQ_7_0
                // Use CachedString parameters to trigger BasicPublishAsyncCachedStringsIntegration
                var cachedExchange = new CachedString(publishExchangeName);
                var cachedRoutingKey = new CachedString(publishRoutingKey);
                await channel.BasicPublishAsync(
                    exchange: cachedExchange,
                    routingKey: cachedRoutingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body);
#else
                await Helper.BasicPublishAsync(channel, publishExchangeName, publishRoutingKey, body, properties);
#endif

                Console.WriteLine($"BasicPublish - Sent message: {message}");
            }

            // Immediately get a message from the queue
            // Move this outside of the manual span to ensure that the operation
            // uses the distributed tracing context
            var result = await Helper.BasicGetAsync(channel, publishQueueName);
#if RABBITMQ_6_0 || RABBITMQ_7_0
            var resultMessage = Encoding.UTF8.GetString(result.Body.ToArray());
#else
                var resultMessage = Encoding.UTF8.GetString(result.Body);
#endif
            Console.WriteLine($"[Program.PublishAndGetDefault] BasicGet - Received message: {resultMessage}");

            if (result.BasicProperties.MessageId != messageId)
            {
                throw new Exception("MessageId was not preserved in BasicProperties");
            }

            if (result.BasicProperties.Headers is null ||
                !result.BasicProperties.Headers.TryGetValue(customHeaderName, out var receivedHeaderValue) ||
                receivedHeaderValue is not byte[] receivedHeaderValueString ||
                Encoding.UTF8.GetString(receivedHeaderValueString) != headerValue)
            {
                throw new Exception("Custom header was not preserved in BasicProperties");
            }
        }

        private static async Task Send(IConnection connection, string consumerType)
        {
            // Configure and send to RabbitMQ queue
            using (var channel = await Helper.CreateChannelAsync(connection))
            {
                await Helper.QueueDeclareAsync(channel, queue: "hello");
                await Helper.QueuePurgeAsync(channel, "hello"); // Ensure there are no more messages in this queue

                for (int i = 0; i < 3; i++)
                {
                    using (SampleHelpers.CreateScope($"PublishToConsumer({consumerType}, i: {i})"))
                    {
                        string message = $"Send - Message #{i}";
                        var body = Encoding.UTF8.GetBytes(message);

#if RABBITMQ_7_0
                        // Use CachedString parameters to trigger BasicPublishAsyncCachedStringsIntegration
                        var cachedExchange = new CachedString("");
                        var cachedRoutingKey = new CachedString("hello");
                        await channel.BasicPublishAsync(
                            exchange: cachedExchange,
                            routingKey: cachedRoutingKey,
                            body: body);
#else
                        await Helper.BasicPublishAsync (channel, exchange: "",
                                             routingKey: "hello",
                                             body: body);
#endif
                        Console.WriteLine("[Send] - [x] Sent \"{0}\"", message);


                        Interlocked.Increment(ref _messageCount);
                    }
                }
            }

            _sendFinished.Set();
            Console.WriteLine("[Send] Exiting Thread.");
        }

        private static async Task Receive(IConnection connection, bool useQueue, ConsumerType consumerType, bool isAsyncConsumer)
        {
            // Let's just wait for all sending activity to finish before doing any work
            _sendFinished.WaitOne();

            // Configure and listen to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };

#if RABBITMQ_5_0 && !RABBITMQ_7_0
            factory.DispatchConsumersAsync = isAsyncConsumer;
#else
            _ = isAsyncConsumer; // not used in v7+
#endif
            using (var channel = await Helper.CreateChannelAsync(connection))

            {
                await Helper.QueueDeclareAsync(channel, queue: "hello");

                var queue = new BlockingCollection<BasicDeliverEventArgs>();
                var consumer = CreateConsumer(channel, queue, useQueue: useQueue, consumerType);
                await Helper.BasicConsumeAsync(channel, "hello", consumer);

                ProcessReceive(useQueue, queue);
            }
        }

#if RABBITMQ_7_0
        private static IAsyncBasicConsumer CreateConsumer(IChannel channel, BlockingCollection<BasicDeliverEventArgs> queue, bool useQueue, ConsumerType consumerType)
        {
            Task HandleEvent(object sender, BasicDeliverEventArgs ea)
            {
                if (useQueue)
                {
                    queue.Add(ea);
                }
                else
                {
                    TraceOnTheReceivingEnd(ea);
                }
                return Task.CompletedTask;
            }

            IAsyncBasicConsumer consumer = null;
            switch (consumerType)
            {
                case ConsumerType.ExternalImplicit:
                    var customSyncConsumer = new Samples.RabbitMQ.AsyncImplicitImplementationConsumer(channel);
                    customSyncConsumer.ReceivedAsync += HandleEvent;
                    consumer = customSyncConsumer;
                    break;
                case ConsumerType.ExternalExplicit:
                    var customAsyncConsumer = new Samples.RabbitMQ.AsyncExplicitImplementationConsumer(channel);
                    customAsyncConsumer.ReceivedAsync += HandleEvent;
                    consumer = customAsyncConsumer;
                    break;
                // By default use the EventingBasicConsumer so we don't have to change span expectations across library versions
                case ConsumerType.InternalAsyncDerived:
                case ConsumerType.InternalSyncDerived:
                default:
                    var defaultConsumer = new global::RabbitMQ.Client.Events.AsyncEventingBasicConsumer(channel);
                    defaultConsumer.ReceivedAsync += HandleEvent;
                    consumer = defaultConsumer;
                    break;
            }

            return consumer;
        }
#else
        private static IBasicConsumer CreateConsumer(IModel channel, BlockingCollection<BasicDeliverEventArgs> queue, bool useQueue, ConsumerType consumerType)
        {
            void HandleEvent(object sender, BasicDeliverEventArgs ea)
            {
                if (useQueue)
                {
                    queue.Add(ea);
                }
                else
                {
                    TraceOnTheReceivingEnd(ea);
                }
            }

            IBasicConsumer consumer = null;
            switch (consumerType)
            {
                case ConsumerType.InternalSyncDerived:
                    var eventingBasicConsumer = new global::RabbitMQ.Client.Events.EventingBasicConsumer(channel);
                    eventingBasicConsumer.Received += HandleEvent;
                    consumer = eventingBasicConsumer;
                    break;
#if RABBITMQ_5_0
                case ConsumerType.InternalAsyncDerived:
                    var asyncEventingBasicConsumer = new global::RabbitMQ.Client.Events.AsyncEventingBasicConsumer(channel);
                    asyncEventingBasicConsumer.Received += (ch, ea) =>
                    {
                        HandleEvent(ch, ea);
                        return Task.CompletedTask;
                    };
                    consumer = asyncEventingBasicConsumer;
                    break;
#endif
                case ConsumerType.ExternalImplicit:
                    var customSyncConsumer = new Samples.RabbitMQ.SyncImplicitImplementationConsumer(channel);
                    customSyncConsumer.Received += HandleEvent;
                    consumer = customSyncConsumer;
                    break;
#if RABBITMQ_5_0
                case ConsumerType.ExternalExplicit:
                    var customAsyncConsumer = new Samples.RabbitMQ.AsyncExplicitImplementationConsumer(channel);
                    customAsyncConsumer.Received += HandleEvent;
                    consumer = customAsyncConsumer;
                    break;
#endif
                // By default use the EventingBasicConsumer so we don't have to change span expectations across library versions
                default:
                    var defaultConsumer = new global::RabbitMQ.Client.Events.EventingBasicConsumer(channel);
                    defaultConsumer.Received += HandleEvent;
                    consumer = defaultConsumer;
                    break;
            }

            return consumer;
        }
#endif

        private static void ProcessReceive(bool useQueue, BlockingCollection<BasicDeliverEventArgs> queue)
        {
            Thread dequeueThread = null;
            if (useQueue)
            {
                dequeueThread = new Thread(() => ConsumeFromQueue(queue));
                dequeueThread.Start();
            }

            while (Interlocked.Read(ref _messageCount) != 0)
            {
                Thread.Sleep(100);
            }

            queue.CompleteAdding();
            if (useQueue)
            {
                dequeueThread.Join();
            }

            Console.WriteLine("[Receive] Exiting Thread.");
        }

        private static void ConsumeFromQueue(BlockingCollection<BasicDeliverEventArgs> queue)
        {
            foreach (var ea in queue.GetConsumingEnumerable())
            {
                TraceOnTheReceivingEnd(ea);
            }
        }

        private static void TraceOnTheReceivingEnd(BasicDeliverEventArgs ea)
        {
#if RABBITMQ_6_0
            var body = ea.Body.ToArray();
#else
            var body = ea.Body;
#endif
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine("[Receive] - [x] Received {0}", message);
            Interlocked.Decrement(ref _messageCount);

            var messageHeaders = ea.BasicProperties?.Headers;

            using (SampleHelpers.CreateScopeWithPropagation("consumer.Received event", messageHeaders, (h, s) => GetValues(messageHeaders, s)))
            {
                Console.WriteLine("created manual span");
            }

            IEnumerable<string> GetValues(IDictionary<string, object> headers, string name)
            {
                if (headers.TryGetValue(name, out object value) && value is byte[] bytes)
                {
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }

                return Enumerable.Empty<string>();
            }
        }

        enum ConsumerType
        {
            InternalSyncDerived,
            ExternalImplicit,
            ExternalExplicit,
            InternalAsyncDerived,
        }
    }

    /// <summary>
    /// A wrapper that encapsulates differences in APIs (primarily sync vs async) across RabbitMQ versions
    /// </summary>
    internal static class Helper
    {
        public static Task<IConnection> CreateConnectionAsync(ConnectionFactory factory)
        {
#if RABBITMQ_7_0
            return factory.CreateConnectionAsync();
#else
            return Task.FromResult(factory.CreateConnection());
#endif
        }

        public static Task<IRabbitChannel> CreateChannelAsync(IConnection connection)
        {
#if RABBITMQ_7_0
            return connection.CreateChannelAsync();
#else
            return Task.FromResult(connection.CreateModel());
#endif
        }

        public static Task<QueueDeclareOk> QueueDeclareAsync(IRabbitChannel channel)
        {
#if RABBITMQ_7_0
            return channel.QueueDeclareAsync();
#else
            return Task.FromResult(channel.QueueDeclare());
#endif
        }


        public static Task<QueueDeclareOk> QueueDeclareAsync(IRabbitChannel channel, string queue)
        {
#if RABBITMQ_7_0
            return channel.QueueDeclareAsync(
                queue: queue,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
#else
            return Task.FromResult(
                channel.QueueDeclare(
                    queue: queue,
                    durable: false,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null));
#endif
        }

        public static Task ExchangeDeclareAsync(IRabbitChannel channel, string exchange, string type)
        {
#if RABBITMQ_7_0
            return channel.ExchangeDeclareAsync(exchange, type);
#else
            channel.ExchangeDeclare(exchange, type);
            return Task.CompletedTask;
#endif
        }

        public static Task QueueBindAsync(IRabbitChannel channel, string publishQueueName, string publishExchangeName, string publishRoutingKey)
        {
#if RABBITMQ_7_0
            return channel.QueueBindAsync(publishQueueName, publishExchangeName, publishRoutingKey);
#else
            channel.QueueBind(publishQueueName, publishExchangeName, publishRoutingKey);
            return Task.CompletedTask;
#endif
        }

        public static Task QueuePurgeAsync(IRabbitChannel channel, string publishQueueName)
        {
#if RABBITMQ_7_0
            return channel.QueuePurgeAsync(publishQueueName);
#else
            channel.QueuePurge(publishQueueName);
            return Task.CompletedTask;
#endif
        }

        public static Task<BasicGetResult> BasicGetAsync(IRabbitChannel channel, string publishQueueName)
        {
#if RABBITMQ_7_0
            return channel.BasicGetAsync(publishQueueName, true);
#else
            return Task.FromResult(channel.BasicGet(publishQueueName, true));
#endif
        }

#if RABBITMQ_6_0 || RABBITMQ_7_0
        public static Task BasicPublishAsync(IRabbitChannel channel, string exchange, string routingKey, ReadOnlyMemory<byte> body)
#else
        public static Task BasicPublishAsync(IRabbitChannel channel, string exchange, string routingKey, byte[] body)
#endif
        {
#if RABBITMQ_7_0
            return channel.BasicPublishAsync(
                               exchange: exchange,
                               routingKey: routingKey,
                               body: body)
                          .AsTask();
#else
            channel.BasicPublish(exchange: exchange,
                                 routingKey: routingKey,
                                 basicProperties: null,
                                 body: body);
            return Task.CompletedTask;
#endif
        }

#if RABBITMQ_6_0 || RABBITMQ_7_0
        public static Task BasicPublishAsync(IRabbitChannel channel, string exchange, string routingKey, ReadOnlyMemory<byte> body, RabbitProperties basicProperties = null)
#else
        public static Task BasicPublishAsync(IRabbitChannel channel, string exchange, string routingKey, byte[] body, RabbitProperties basicProperties = null)
#endif
        {
#if RABBITMQ_7_0
            return channel.BasicPublishAsync(
                               exchange: exchange,
                               routingKey: routingKey,
                               mandatory: false,
                               basicProperties: basicProperties,
                               body: body)
                          .AsTask();
#else
            channel.BasicPublish(exchange: exchange,
                                 routingKey: routingKey,
                                 basicProperties: basicProperties,
                                 body: body);
            return Task.CompletedTask;
#endif
        }

        public static Task BasicConsumeAsync(IRabbitChannel channel, string queue, IRabbitConsumer consumer)
        {
#if RABBITMQ_7_0
            return channel.BasicConsumeAsync(queue, autoAck: true, consumer);
#else
            channel.BasicConsume(queue, true, consumer);
            return Task.CompletedTask;
#endif
        }

        public static RabbitProperties CreateBasicProperties(IRabbitChannel channel)
        {
#if RABBITMQ_7_0
            return new BasicProperties();
#else
            return channel.CreateBasicProperties();
#endif
        }
    }
}
