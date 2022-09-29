using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Globalization;
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
                    // Automatic instrumentation generates a span with resource "queue.declare"
                    channel.QueueDeclare(queue: "hello",
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    // You can use the built-in consumer type
                    var consumer = new EventingBasicConsumer(channel);
                    // Or you can use a custom consumer that implements IBasicConsumer
                    //var consumer = new CustomInstrumentationConsumer(channel);

                    // Automatic instrumentation generates a span with resource "basic.deliver"
                    consumer.Received += (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        Console.WriteLine(" [x] Received {0}", message);

                        // Perform work
                        Thread.Sleep(100);
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
