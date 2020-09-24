using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace;
using RabbitMQ.Client;

namespace Send
{
    class Send
    {
        // This small application follows the RabbitMQ tutorial at https://www.rabbitmq.com/tutorials/tutorial-one-dotnet.html
        public static void Main(string[] args)
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

                    using (var scope = Tracer.Instance.StartActive("rabbitmq-publish"))
                    {
                        string message = "Hello World!";
                        var body = Encoding.UTF8.GetBytes(message);

                        // Add basic properties
                        // This is where we'll manually add the datadog headers:
                        //  - "x-datadog-trace-id": "<trace_id>"
                        //  - "x-dataadog-span-id": "<span_id>"
                        var properties = channel.CreateBasicProperties();
                        properties.Headers = new Dictionary<string, object>();
                        properties.Headers.Add("x-datadog-trace-id", CorrelationIdentifier.TraceId.ToString());
                        properties.Headers.Add("x-datadog-parent-id", CorrelationIdentifier.SpanId.ToString());

                        channel.BasicPublish(exchange: "",
                                            routingKey: "hello",
                                            basicProperties: properties,
                                            body: body);
                        Console.WriteLine(" [x] Sent {0}", message);
                    }
                }

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }
    }
}
