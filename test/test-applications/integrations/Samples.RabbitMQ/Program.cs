using System;
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

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        }

        public static void Main(string[] args)
        {
            PublishAndGet();
            PublishAndGetDefault();

            var sendThread = new Thread(Send);
            sendThread.Start();

            var receiveThread = new Thread(Receive);
            receiveThread.Start();

            sendThread.Join();
            receiveThread.Join();
        }

        private static void PublishAndGet()
        {
            // Configure and send to RabbitMQ queue
            var factory = new ConnectionFactory() { HostName = Host() };

            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                using (Tracer.Instance.StartActive("PublishAndGet()"))
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

                using (Tracer.Instance.StartActive("PublishAndGetDefault()"))
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
                    using (Tracer.Instance.StartActive("PublishToConsumer()"))
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

        private static void Receive()
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
                    using (Tracer.Instance.StartActive("consumer.Received event", serviceName: "Samples.RabbitMQ"))
                    {
#if RABBITMQ_6_0
                        var body = ea.Body.ToArray();
#else
                        var body = ea.Body;
#endif

                        var message = Encoding.UTF8.GetString(body);
                        Console.WriteLine("[Receive] - [x] Received {0}", message);

                        _messageCount -= 1;
                    }
                };
                channel.BasicConsume("hello",
                                    true,
                                    consumer);

                while (_messageCount != 0)
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine("[Receive] Exiting Thread.");
            }
        }
    }
}
