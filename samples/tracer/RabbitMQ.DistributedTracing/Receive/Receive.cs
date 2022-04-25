using Datadog.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Receive
{
    class Receive
    {
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
                        // Receive message
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        // Read the basic property headers and extract the Datadog properties
                        var headers = ea.BasicProperties.Headers;
                        var contextPropagator = new SpanContextExtractor();
                        var spanContext = contextPropagator.Extract(headers, (h, s) => GetValues(headers, s));
                        var spanCreationSettings = new SpanCreationSettings() { Parent = spanContext };

                        // Start a new Datadog span
                        using (var scope = Tracer.Instance.StartActive("rabbitmq.consume", spanCreationSettings))
                        {
                            // Log message and properties to screen
                            Console.WriteLine(" [x] Received.");
                            Console.WriteLine("     Message: {0}", message);
                            Console.WriteLine("     Active TraceId: {0}", scope.Span.TraceId);
                            Console.WriteLine("     Active SpanId: {0}", scope.Span.SpanId);
                            Console.WriteLine("     Active SamplingPriority: {0}", scope.Span.GetTag(Tags.SamplingPriority));

                            // Set Datadog tags
                            var span = scope.Span;
                            span.SetTag(Tags.SpanKind, SpanKinds.Consumer);
                            span.SetTag("amqp.exchange", ea.Exchange);
                            span.SetTag("amqp.routing_key", ea.RoutingKey);

                            // Do work inside the Datadog trace
                            Thread.Sleep(1000);
                        }
                    };
                    channel.BasicConsume(queue: "hello",
                                         autoAck: true,
                                         consumer: consumer);

                    Console.WriteLine(" Press [enter] to exit.");
                    Console.ReadLine();
                }
            }
        }

        private static IEnumerable<string> GetValues(IDictionary<string, object> headers, string name)
        {
            if (headers.TryGetValue(name, out object value) && value is byte[] bytes)
            {
                return new[] { Encoding.UTF8.GetString(bytes) };
            }

            return Enumerable.Empty<string>();
        }
    }
}
