using Datadog.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Globalization;
using System.Text;

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
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        // Read the basic property headers and extract the Datadog properties
                        var headers = ea.BasicProperties.Headers;
                        ulong? parentSpanId = null;
                        ulong? traceId = null;
                        SamplingPriority? samplingPriority = null;

                        // Parse parentId
                        if (headers?[HttpHeaderNames.ParentId] is byte[] parentSpanIdBytes)
                        {
                            var parentSpanIdString = Encoding.UTF8.GetString(parentSpanIdBytes);
                            if (ulong.TryParse(parentSpanIdString, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                            {
                                parentSpanId = result;
                            }
                        }

                        // Parse traceId
                        if (headers?[HttpHeaderNames.TraceId] is byte[] traceIdBytes)
                        {
                            var traceIdString = Encoding.UTF8.GetString(traceIdBytes);
                            if (ulong.TryParse(traceIdString, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                            {
                                traceId = result;
                            }
                        }

                        // Parse samplingPriority
                        if (headers?[HttpHeaderNames.SamplingPriority] is byte[] samplingPriorityBytes)
                        {
                            var samplingPriorityString = Encoding.UTF8.GetString(samplingPriorityBytes);
                            if (Enum.TryParse<SamplingPriority>(samplingPriorityString, out var result))
                            {
                                samplingPriority = result;
                            }
                        }

                        // Create a new SpanContext to represent the distributed tracing information
                        SpanContext propagatedContext = null;
                        if (parentSpanId.HasValue && traceId.HasValue)
                        {
                            propagatedContext = new SpanContext(traceId, parentSpanId.Value, samplingPriority);
                        }

                        // Start a new Datadog span
                        using (var scope = Tracer.Instance.StartActive("rabbitmq-consume", propagatedContext))
                        {
                            Console.WriteLine(" [x] Received.");
                            Console.WriteLine("     Message: {0}", message);
                            Console.WriteLine("     Active TraceId: {0}", scope.Span.TraceId);
                            Console.WriteLine("     Active SpanId: {0}", scope.Span.SpanId);
                            Console.WriteLine("     Active SamplingPriority: {0}", scope.Span.GetTag(Tags.SamplingPriority));
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
    }
}
