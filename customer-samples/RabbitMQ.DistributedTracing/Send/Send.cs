using System;
using System.Collections.Generic;
using System.Globalization;
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

                    // Start a new Datadog span
                    using (var scope = Tracer.Instance.StartActive("rabbitmq.publish"))
                    {
                        string message = "Hello World!";
                        var body = Encoding.UTF8.GetBytes(message);

                        // Create BasicProperties and a Headers dictionary to store header information
                        var properties = channel.CreateBasicProperties();
                        properties.Headers = new Dictionary<string, object>();

                        // Get properties for the active Datadog span
                        ulong traceId = scope.Span.TraceId;
                        ulong spanId = scope.Span.SpanId;
                        string samplingPriority = scope.Span.GetTag(Tags.SamplingPriority);

                        // Add properties to the Headers dictionary in the following way:
                        //  - "x-datadog-trace-id": "<trace_id>"
                        //  - "x-datadog-parent-id": "<span_id>"
                        //  - "x-datadog-sampling-priority": "<sampling_priority>"
                        properties.Headers.Add(HttpHeaderNames.TraceId, BitConverter.GetBytes(traceId));
                        properties.Headers.Add(HttpHeaderNames.ParentId, BitConverter.GetBytes(spanId));
                        properties.Headers.Add(HttpHeaderNames.SamplingPriority, Encoding.UTF8.GetBytes(samplingPriority));

                        // Publish message
                        channel.BasicPublish(exchange: "",
                                             routingKey: "hello",
                                             basicProperties: properties, // Pass the properties with the message
                                             body: body);

                        // Log message and properties to screen
                        Console.WriteLine(" [x] Sent {0}", message);
                        Console.WriteLine("     {0}:{1}", HttpHeaderNames.TraceId, traceId);
                        Console.WriteLine("     {0}:{1}", HttpHeaderNames.ParentId, spanId);
                        Console.WriteLine("     {0}:{1}", HttpHeaderNames.SamplingPriority, samplingPriority);

                        // Set Datadog tags
                        var span = scope.Span;
                        span.ResourceName = "basic.publish";
                        span.SetTag(Tags.SpanKind, SpanKinds.Producer);
                        span.SetTag("amqp.exchange", "");
                        span.SetTag("amqp.routing_key", "hello");
                    }
                }

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }
    }
}
