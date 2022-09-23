using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ
{
    public static class Program
    {
        static long _messageCount = 0;
        static AutoResetEvent _sendFinished = new AutoResetEvent(false);

        private static readonly string exchangeName = "test-exchange-name";
        private static readonly string routingKey = "test-routing-key";
        private static readonly string queueName = "test-queue-name";

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        }

        public static void Main(string[] args)
        {
            RunRabbitMQ();
        }

        private static void RunRabbitMQ()
        {
            RunProducersAndConsumers(useQueue: false, ConsumerType.SyncImplicitInternal, isAsyncConsumer: false);

            // Doing the test twice to make sure that both our context propagation works but also manual propagation (when users enqueue messages for instance)
            RunProducersAndConsumers(useQueue: true, ConsumerType.SyncImplicitExternal, isAsyncConsumer: false);

            // ConsumerType.AsyncExplicitExternal -- Need to test interface instrumentation async basic deliver
            RunProducersAndConsumers(useQueue: true, ConsumerType.AsyncExplicitExternal, isAsyncConsumer: true);

            // ConsumerType.AsyncDerived -- Need to test derived instrumentation async basic deliver
            RunProducersAndConsumers(useQueue: true, ConsumerType.AsyncDerivedInternal, isAsyncConsumer: true);
        }

        private static void RunProducersAndConsumers(bool useQueue, ConsumerType consumerType, bool isAsyncConsumer)
        {
            PublishAndGet(useDefaultQueue: false);
            PublishAndGet(useDefaultQueue: true);

            var sendThread = new Thread(Send);
            sendThread.Start();

            var receiveThread = new Thread(o => Receive(useQueue, consumerType, isAsyncConsumer));
            receiveThread.Start();

            sendThread.Join();
            receiveThread.Join();

            _sendFinished.Reset();
        }

        private static void PublishAndGet(bool useDefaultQueue)
        {
            string messagePrefix = $"Program.PublishAndGetDefault(useDefaultQueue: {useDefaultQueue})";

            // Configure and send to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };
            
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                string publishExchangeName;
                string publishQueueName;
                string publishRoutingKey;

                using (SampleHelpers.CreateScope(messagePrefix))
                {
                    if (useDefaultQueue)
                    {
                        publishExchangeName = "";
                        publishQueueName = channel.QueueDeclare().QueueName;
                        publishRoutingKey = publishQueueName;
                    }
                    else
                    {
                        publishExchangeName = exchangeName;
                        publishQueueName = queueName;
                        publishRoutingKey = routingKey;

                        channel.ExchangeDeclare(publishExchangeName, "direct");
                        channel.QueueDeclare(queue: publishQueueName,
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: null);
                        channel.QueueBind(publishQueueName, publishExchangeName, publishRoutingKey);
                    }

                    // Ensure there are no more messages in this queue
                    channel.QueuePurge(publishQueueName);

                    // Test an empty BasicGetResult
                    channel.BasicGet(publishQueueName, true);

                    // Send message to the default exchange and use new queue as the routingKey
                    string message = $"{messagePrefix} - Message";
                    var body = Encoding.UTF8.GetBytes(message);
                    channel.BasicPublish(exchange: publishExchangeName,
                                            routingKey: publishRoutingKey,
                                            basicProperties: null,
                                            body: body);
                    Console.WriteLine($"BasicPublish - Sent message: {message}");
                }

                // Immediately get a message from the queue
                // Move this outside of the manual span to ensure that the operation
                // uses the distributed tracing context
                var result = channel.BasicGet(publishQueueName, true);
#if RABBITMQ_6_0
                var resultMessage = Encoding.UTF8.GetString(result.Body.ToArray());
#else
                var resultMessage = Encoding.UTF8.GetString(result.Body);
#endif

                Console.WriteLine($"[Program.PublishAndGetDefault] BasicGet - Received message: {resultMessage}");
            }
        }

        private static void Send()
        {
            // Configure and send to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };
            using(var connection = factory.CreateConnection())
            using(var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "hello",
                                        durable: false,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null);
                channel.QueuePurge("hello"); // Ensure there are no more messages in this queue

                for (int i = 0; i < 3; i++)
                {
                    using (SampleHelpers.CreateScope("PublishToConsumer()"))
                    {
                        string message = $"Send - Message #{i}";
                        var body = Encoding.UTF8.GetBytes(message);

                        channel.BasicPublish(exchange: "",
                                                routingKey: "hello",
                                                basicProperties: null,
                                                body: body);
                        Console.WriteLine("[Send] - [x] Sent \"{0}\"", message);


                        Interlocked.Increment(ref _messageCount);
                    }
                }
            }

            _sendFinished.Set();
            Console.WriteLine("[Send] Exiting Thread.");
        }

        private static void Receive(bool useQueue, ConsumerType consumerType, bool isAsyncConsumer)
        {
            // Let's just wait for all sending activity to finish before doing any work
            _sendFinished.WaitOne();

            // Configure and listen to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };
            factory.DispatchConsumersAsync = isAsyncConsumer;

            using(var connection = factory.CreateConnection())
            using(var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "hello",
                                    durable: false,
                                    exclusive: false,
                                    autoDelete: false,
                                    arguments: null);

                var queue = new BlockingCollection<BasicDeliverEventArgs>();
                var consumer = CreateConsumer(channel, queue, useQueue: useQueue, consumerType);
                channel.BasicConsume("hello",
                                    true,
                                    consumer);

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
        }

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
                case ConsumerType.SyncImplicitInternal:
                    var eventingBasicConsumer = new global::RabbitMQ.Client.Events.EventingBasicConsumer(channel);
                    eventingBasicConsumer.Received += HandleEvent;
                    consumer = eventingBasicConsumer;
                    break;
                case ConsumerType.SyncImplicitExternal:
                    var customImplicitConsumer = new Samples.RabbitMQ.SyncImplicitImplementationConsumer(channel);
                    customImplicitConsumer.Received += HandleEvent;
                    consumer = customImplicitConsumer;
                    break;
                case ConsumerType.AsyncExplicitExternal:
                    var asyncExplicitExternalConsumer = new Samples.RabbitMQ.AsyncExplicitImplementationConsumer(channel);
                    asyncExplicitExternalConsumer.Received += HandleEvent;
                    consumer = asyncExplicitExternalConsumer;
                    break;
                case ConsumerType.AsyncDerivedInternal:
                    var asyncDerivedInternalConsumer = new global::RabbitMQ.Client.Events.AsyncEventingBasicConsumer(channel);
                    asyncDerivedInternalConsumer.Received += async (ch, ea) =>
                    {
                        HandleEvent(ch, ea);
                        await Task.Yield();
                    };
                    consumer = asyncDerivedInternalConsumer;
                    break;
                default:
                    break;
            }

            return consumer;
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
            SyncImplicitInternal,
            SyncImplicitExternal,
            SyncDerivedInternal,
            SyncDerivedExternal,
            AsyncExplicitExternal,
            AsyncDerivedInternal,
            AsyncDerivedExternal
        }

        class ImplicitImplementationDerivedConsumer : AsyncDefaultBasicConsumer
        {
            public ImplicitImplementationDerivedConsumer(IModel model)
                : base(model)
            {
            }

            public event EventHandler<BasicDeliverEventArgs> Received;

            public override async Task HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                await Task.Yield();
                Received?.Invoke(
                    this,
                    new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
            }
        }
    }
}
