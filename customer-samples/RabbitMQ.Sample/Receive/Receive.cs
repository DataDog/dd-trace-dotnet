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

                        // Read the basic property headers and extract the Datadog spanId and traceId
                        var headers = ea.BasicProperties.Headers;
                        ulong? spanId = null;
                        ulong? traceId = null;

                        // Parse spanId
                        var spanIdBytes = (byte[]) headers["x-datadog-parent-id"];
                        var spanIdString = Encoding.UTF8.GetString(spanIdBytes);
                        spanId = ulong.Parse(spanIdString, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture);

                        // Parse traceId
                        var traceIdBytes = (byte[]) headers["x-datadog-trace-id"];
                        var traceIdString = Encoding.UTF8.GetString(traceIdBytes);
                        traceId = ulong.Parse(traceIdString, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture);

                        // Create a new SpanContext to represent the distributed tracing information
                        SpanContext propagatedContext = null;
                        if (spanId.HasValue && traceId.HasValue)
                        {
                            propagatedContext = new SpanContext(traceId, spanId.Value);
                        }

                        using (var scope = Tracer.Instance.StartActive("rabbitmq-consume", propagatedContext))
                        {
                            Console.WriteLine(" [x] Received.");
                            Console.WriteLine("     Message: {0}", message);
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
