using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Samples.RabbitMQ
{
    public static class Program
    {
        static volatile int _messageCount = 0;
        static AutoResetEvent _sendFinished = new AutoResetEvent(false);

        private static readonly string exchangeName = "test-exchange-name";
        private static readonly string routingKey = "test-routing-key";
        private static readonly string queueName = "test-queue-name";

        private static readonly ConcurrentQueue<BasicDeliverEventArgs> _queue = new ();
        private static readonly Thread DequeueThread = new Thread(ConsumeFromQueue);
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
            PublishAndGet();
            PublishAndGetDefault();

            var sendThread = new Thread(Send);
            sendThread.Start();

            var receiveThread = new Thread(o => Receive(false));
            receiveThread.Start();

            sendThread.Join();
            receiveThread.Join();

            // Doing the test twice to make sure that both our context propagation works but also manual propagation (when users enqueue messages for instance)
            PublishAndGet();
            PublishAndGetDefault();

            sendThread = new Thread(Send);
            sendThread.Start();

            receiveThread = new Thread(o => Receive(true));
            receiveThread.Start();

            sendThread.Join();
            receiveThread.Join();
            DequeueThread.Join();

        }

        private static void PublishAndGet()
        {
            // Configure and send to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                using (SampleHelpers.CreateScope("PublishAndGet()"))
                {
                    channel.ExchangeDeclare(exchangeName, "direct");
                    channel.QueueDeclare(queue: queueName,
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false,
                                            arguments: null);
                    channel.QueueBind(queueName, exchangeName, routingKey);
                    channel.QueuePurge(queueName); // Ensure there are no more messages in this queue

                    // Test an empty BasicGetResult
                    channel.BasicGet(queueName, true);

                    // Send message to the exchange
                    byte[] body = null;

                    channel.BasicPublish(exchange: exchangeName,
                                            routingKey: routingKey,
                                            basicProperties: null,
                                            body: body);
                    Console.WriteLine($"[Program.PublishAndGet] BasicPublish - Sent message: {string.Empty}");
                }

                // Immediately get a message from the queue (bound to the exchange)
                // Move this outside of the manual span to ensure that the operation
                // uses the distributed tracing context
                var result = channel.BasicGet(queueName, true);
#if RABBITMQ_6_0
                var resultMessage = Encoding.UTF8.GetString(result.Body.ToArray());
#else
                var resultMessage = Encoding.UTF8.GetString(result.Body);
#endif

                Console.WriteLine($"[Program.PublishAndGet] BasicGet - Received message: {resultMessage}");
            }
        }

        private static void PublishAndGetDefault()
        {
            // Configure and send to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };
            
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                string defaultQueueName;

                using (SampleHelpers.CreateScope("PublishAndGetDefault()"))
                {
                    defaultQueueName = channel.QueueDeclare().QueueName;
                    channel.QueuePurge(queueName); // Ensure there are no more messages in this queue

                    // Test an empty BasicGetResult
                    channel.BasicGet(defaultQueueName, true);

                    // Send message to the default exchange and use new queue as the routingKey
                    string message = "PublishAndGetDefault - Message";
                    var body = Encoding.UTF8.GetBytes(message);
                    channel.BasicPublish(exchange: "",
                                            routingKey: defaultQueueName,
                                            basicProperties: null,
                                            body: body);
                    Console.WriteLine($"[Program.PublishAndGetDefault] BasicPublish - Sent message: {message}");
                }

                // Immediately get a message from the queue
                // Move this outside of the manual span to ensure that the operation
                // uses the distributed tracing context
                var result = channel.BasicGet(defaultQueueName, true);
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


                        _messageCount += 1;
                    }
                }
            }

            _sendFinished.Set();
            Console.WriteLine("[Send] Exiting Thread.");
        }

        private static void Receive(bool useQueue)
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

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += (model, ea) =>
                {
                    if (useQueue)
                    {
                        _queue.Enqueue(ea);
                    }
                    else
                    {
                        TraceOnTheReceivingEnd(ea);
                    }
                };
                channel.BasicConsume("hello",
                                    true,
                                    consumer);

                if (useQueue)
                {
                    DequeueThread.Start();
                }

                while (_messageCount != 0)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("[Receive] Exiting Thread.");
            }
        }

        private static void ConsumeFromQueue()
        {
            while (_queue.Count > 0 )
            {
                if (_queue.TryDequeue(out var ea))
                {
                    TraceOnTheReceivingEnd(ea);
                }
                Thread.Sleep(100);
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
            _messageCount -= 1;

            var messageHeaders = ea.BasicProperties?.Headers;
            var contextPropagator = new SpanContextExtractor();
            var spanContext = contextPropagator.Extract(messageHeaders, (h, s) => GetValues(messageHeaders, s));
            var spanCreationSettings = new SpanCreationSettings() { Parent = spanContext };

            if (spanContext is null || spanContext.TraceId is 0 || spanContext.SpanId is 0)
            {
                // For kafka brokers < 0.11.0, we can't inject custom headers, so context will not be propagated
                var errorMessage = $"Error extracting trace context for {message}";
                Console.WriteLine(errorMessage);
            }
            else
            {
                Console.WriteLine($"Successfully extracted trace context from message: {spanContext.TraceId}, {spanContext.SpanId}");
            }

            IEnumerable<string> GetValues(IDictionary<string, object> headers, string name)
            {
                if (headers.TryGetValue(name, out object value) && value is byte[] bytes)
                {
                    return new[] { Encoding.UTF8.GetString(bytes) };
                }

                return Enumerable.Empty<string>();
            }

            using var scope = Tracer.Instance.StartActive("consumer.Received event", spanCreationSettings);
        }
    }
}
