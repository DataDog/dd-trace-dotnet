using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EasyNetQ;

namespace Samples.EasyNetQ
{
    public static class Program
    {
        private static readonly string exchangeName = "easynetqtest-exchange-name";
        private static readonly string routingKey = "easynetqtest-routing-key";
        private static readonly string queueName = "easynetqtest-queue-name";

        private static string Host()
        {
            return Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        }

        public static void Main(string[] args)
        {
            RunEasyNetQ();
        }

        private static void RunEasyNetQ()
        {
            var bus = RabbitHutch.CreateBus($"host={Host()}").Advanced;

            // gotta get an IAdvancedBus from somewhere

            var exchange = bus.ExchangeDeclare(exchangeName, "direct", durable: false, autoDelete: false);
            var queue = bus.QueueDeclare(name: queueName, 
                                            durable: false,
                                            exclusive: false,
                                            autoDelete: false);
            bus.Bind(exchange, queue, routingKey);
            bus.QueuePurge(queueName);

            // Send message to the exchange
            byte[] body = null;

            bus.Publish(exchange, routingKey, mandatory: false, messageProperties: new(), body);
            Console.WriteLine($"[Program.PublishAndGet] BasicPublish - Sent message: {string.Empty}");

            // Send message to the default exchange and use new queue as the routingKey
            string message = "PublishAndGetDefault - Message";
            body = Encoding.UTF8.GetBytes(message);
            bus.Publish(exchange, routingKey, mandatory: false, messageProperties: new(), body);
            Console.WriteLine($"[Program.PublishAndGetDefault] BasicPublish - Sent message: {message}");

            CountdownEvent cde = new(2);

            var d = bus.Consume(queue, async (body, properties, info) =>
            {
                await Task.Delay(100);
                Console.WriteLine($"Consumed message with RoutingKey={info.RoutingKey} and MessageBody=\"{Encoding.UTF8.GetString(body.ToArray())}\"");
                cde.Signal();
            });

            // The countdown event will be triggered at the end of the Consume callback,
            // before the automatic instrumentation's OnAsyncMethodEnd callback has been invoked.
            // Add a short delay, in addition to the countdown event, to give the span a chance
            // to close and send to the agent.
            cde.Wait();

            Console.WriteLine("Waiting an additional 2 seconds to better ensure the consumer spans close.");
            Thread.Sleep(2000);
        }
    }
}
