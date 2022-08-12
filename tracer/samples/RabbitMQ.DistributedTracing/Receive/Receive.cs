using Datadog.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Receive
{
    class Receive
    {
        static AutoResetEvent _sendFinished = new AutoResetEvent(false);
        static ConcurrentQueue<BasicDeliverEventArgs> _queue = new ConcurrentQueue<BasicDeliverEventArgs>();


        // This small application follows the RabbitMQ tutorial at https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html
        public static void Main()
        {

            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: "hello",
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    var consumer = new EventingBasicConsumer(channel);
                    consumer.Received += (model, ea) =>
                    {
                        // Enqueue locally for later usage.
                        _queue.Enqueue(ea);

                        Console.WriteLine("Enqueueing message. By doing so, losing the distributed context that would have been stored in the async context.");
                        Console.WriteLine("Automatic instrumentation should have added datadog headers:");
                        if (ea.BasicProperties.Headers != null)
                        {
                            foreach (var header in ea.BasicProperties.Headers)
                            {
                                Console.WriteLine($"Name: {header.Key}, Value: {header.Value}");
                            }
                        }
                    };


                    channel.BasicConsume(queue: "hello",
                                         autoAck: true,
                                         consumer: consumer);

                    var dequeueThread = new Thread(() => ConsumeFromQueue());
                    dequeueThread.Start();

                    Console.WriteLine(" Press [enter] to exit.");
                    Console.ReadLine();
                    _sendFinished.Set();
                    dequeueThread.Join();
                }
            }


        }

        private static void ConsumeFromQueue()
        {
            while (!_sendFinished.WaitOne(TimeSpan.FromMilliseconds(500)))
            {
                while (_queue.TryDequeue(out var ea))
                {
                    // Receive message
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    // Read the basic property headers and extract the Datadog properties
                    var headers = ea.BasicProperties.Headers;

                    // Log message and properties to screen
                    Console.WriteLine(" [x] Received.");
                    Console.WriteLine("     Message: {0}", message);

                    // extract the context to this message as it has been lost when enqueuing
                    var messageHeaders = ea.BasicProperties?.Headers;
                    if (messageHeaders != null)
                    {
                        var spanContextExtractor = new SpanContextExtractor();
                        var parentContext = spanContextExtractor.Extract(messageHeaders,
                            (headers, key) => GetHeaderValues(headers, key));
                        var spanCreationSettings = new SpanCreationSettings() {Parent = parentContext};

                        // Create child spans
                        using var scope = Tracer.Instance.StartActive("child.span", spanCreationSettings);
                        {
                            Console.WriteLine("In child span context");
                        }
                    }

                    IEnumerable<string> GetHeaderValues(IDictionary<string, object> headers, string name)
                    {
                        if (headers == null)
                            return Enumerable.Empty<string>();

                        if (headers.TryGetValue(name, out object value) && value is byte[] bytes)
                        {
                            return new[] {Encoding.UTF8.GetString(bytes)};
                        }

                        return Enumerable.Empty<string>();
                    }
                }
            }
        }
    }
}
