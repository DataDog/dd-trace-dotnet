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
            RunProducersAndConsumers(useQueue: false, ConsumerType.SameAssemblyImplicitImplementation);

            // Doing the test twice to make sure that both our context propagation works but also manual propagation (when users enqueue messages for instance)
            RunProducersAndConsumers(useQueue: true, ConsumerType.DifferentAssemblyImplicitImplementation);

            // Do the test one more time to ensure that we instrument the various interface method scenarios
            RunProducersAndConsumers(useQueue: true, ConsumerType.DifferentAssemblyExplicitImplementation);
        }

        private static void RunProducersAndConsumers(bool useQueue, ConsumerType consumerType)
        {
            PublishAndGet(useDefaultQueue: false);
            PublishAndGet(useDefaultQueue: true);

            var sendThread = new Thread(Send);
            sendThread.Start();

            var receiveThread = new Thread(o => Receive(useQueue, consumerType));
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

        private static void Receive(bool useQueue, ConsumerType consumerType)
        {
            // Let's just wait for all sending activity to finish before doing any work
            _sendFinished.WaitOne();

            // Configure and listen to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };
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

            IBasicConsumer consumer;
            switch (consumerType)
            {
                case ConsumerType.SameAssemblyImplicitImplementation:
                    var eventingBasicConsumer = new global::RabbitMQ.Client.Events.EventingBasicConsumer(channel);
                    eventingBasicConsumer.Received += HandleEvent;
                    consumer = eventingBasicConsumer;
                    break;
                case ConsumerType.DifferentAssemblyImplicitImplementation:
                    var customImplicitConsumer = new Samples.RabbitMQ.Program.ImplicitImplementationConsumer(channel);
                    customImplicitConsumer.Received += HandleEvent;
                    consumer = customImplicitConsumer;
                    break;
                case ConsumerType.DifferentAssemblyExplicitImplementation:
                default:
                    var customExplicitConsumer = new Samples.RabbitMQ.Program.ExplicitImplementationConsumer(channel);
                    customExplicitConsumer.Received += HandleEvent;
                    consumer = customExplicitConsumer;
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
            SameAssemblyImplicitImplementation,
            DifferentAssemblyImplicitImplementation,
            DifferentAssemblyExplicitImplementation,
        }

        // Implement a custom consumer class whose implementation is nearly
        // identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
        // This will test that we can properly instrument implicit interface implementations
        // of RabbitMQ.Client.IBasicConsumer.HandleBasicDeliver
        class ImplicitImplementationConsumer : IBasicConsumer
        {
            private readonly DefaultBasicConsumer _consumer;
            private readonly IModel _model;

            public ImplicitImplementationConsumer(IModel model)
            {
                _consumer = new DefaultBasicConsumer(model);
                _model = model;
            }

            public IModel Model => _model;

            public event EventHandler<BasicDeliverEventArgs> Received;

            public event EventHandler<ConsumerEventArgs> Registered;

            public event EventHandler<ShutdownEventArgs> Shutdown;

            public event EventHandler<ConsumerEventArgs> Unregistered;

            public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

            public void HandleBasicCancel(string consumerTag)
            {
                _consumer.HandleBasicCancel(consumerTag);
            }

            public void HandleBasicCancelOk(string consumerTag)
            {
                _consumer.HandleBasicCancelOk(consumerTag);
                Unregistered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
            }

            public void HandleBasicConsumeOk(string consumerTag)
            {
                _consumer.HandleBasicConsumeOk(consumerTag);
                Registered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
            }

            public void HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                Received?.Invoke(
                    this,
                    new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
            }

            public void HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                _consumer.HandleModelShutdown(model, reason);
                Shutdown?.Invoke(this, reason);
            }
        }

        // Implement a custom consumer class whose implementation is nearly
        // identical to the implementation of RabbitMQ.Client.Events.EventingBasicConsumer.
        // This will test that we can properly instrument explicit interface implementations
        // of RabbitMQ.Client.IBasicConsumer.HandleBasicDeliver
        class ExplicitImplementationConsumer : IBasicConsumer
        {
            private readonly DefaultBasicConsumer _consumer;
            private readonly IModel _model;

            public ExplicitImplementationConsumer(IModel model)
            {
                _consumer = new DefaultBasicConsumer(model);
                _model = model;
            }

            IModel IBasicConsumer.Model => _model;

            public event EventHandler<BasicDeliverEventArgs> Received;

            public event EventHandler<ConsumerEventArgs> Registered;

            public event EventHandler<ShutdownEventArgs> Shutdown;

            public event EventHandler<ConsumerEventArgs> Unregistered;

            public event EventHandler<ConsumerEventArgs> ConsumerCancelled;

            void IBasicConsumer.HandleBasicCancel(string consumerTag)
            {
                _consumer.HandleBasicCancel(consumerTag);
            }

            void IBasicConsumer.HandleBasicCancelOk(string consumerTag)
            {
                _consumer.HandleBasicCancelOk(consumerTag);
                Unregistered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
            }

            void IBasicConsumer.HandleBasicConsumeOk(string consumerTag)
            {
                _consumer.HandleBasicConsumeOk(consumerTag);
                Registered?.Invoke(this, new ConsumerEventArgs(new[] { consumerTag }));
            }

            void IBasicConsumer.HandleBasicDeliver(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IBasicProperties properties, ReadOnlyMemory<byte> body)
            {
                Received?.Invoke(
                    this,
                    new BasicDeliverEventArgs(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body));
            }

            void IBasicConsumer.HandleModelShutdown(object model, ShutdownEventArgs reason)
            {
                _consumer.HandleModelShutdown(model, reason);
                Shutdown?.Invoke(this, reason);
            }
        }
    }
}
