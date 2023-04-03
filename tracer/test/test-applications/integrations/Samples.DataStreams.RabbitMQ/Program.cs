using System;
using System.Text;
using Datadog.Trace;
using RabbitMQ.Client;

namespace Samples.DataStreams.RabbitMQ
{
    public static class Program
    {
        private static readonly string Queue = nameof(Queue);
        private static readonly string Exchange = nameof(Exchange);
        private static readonly string RoutingKey = nameof(RoutingKey);
        private static readonly string Message = nameof(Message);
        private static readonly string Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        
        public static void Main(string[] args)
        {
            using (Tracer.Instance.StartActive("rabbitmq-sample"))
            {
                var factory = new ConnectionFactory() { HostName = Host };
                using var connection = factory.CreateConnection();
                using var model = connection.CreateModel();
            
                // produce/consume operation (direct exchange)
                PublishMessageToQueue(model, Queue, Message);
                var msg = GetMessage(model, Queue);

                // continuing the chain
                var queueName = PublishMessageToExchange(model, Exchange, RoutingKey, msg);
                GetMessage(model, queueName);              
            }
        }

        private static void PublishMessageToQueue(IModel model, string queue, string message)
        {
            model.QueueDeclare(queue: queue,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueuePurge(queue);

            model.BasicPublish(
                "", 
                queue, 
                null, 
                Encoding.UTF8.GetBytes(message));
            
            Console.WriteLine($"[Sent] {message} to {queue}.");
        }

        private static string PublishMessageToExchange(IModel model, string exchange, string routingKey, string message)
        {
            var publishQueue = $"publish-{exchange}";
            
            model.ExchangeDeclare(exchange, "direct");
            model.QueueDeclare(queue: publishQueue,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueBind(publishQueue, exchange, routingKey);
            model.QueuePurge(publishQueue);

            model.BasicPublish(exchange: exchange,
                               routingKey: routingKey,
                               basicProperties: null,
                               body: Encoding.UTF8.GetBytes(message));
            
            Console.WriteLine($"[Sent] {message} to {exchange} using {routingKey}. Publish queue name is {publishQueue}");
            return publishQueue;
        }

        private static string GetMessage(IModel model, string queue)
        {
            var result = model.BasicGet(queue, true);
#if RABBITMQ_6_0
            var message = Encoding.UTF8.GetString(result.Body.ToArray());
#else
            var message =  Encoding.UTF8.GetString(result.Body);
#endif
            Console.WriteLine($"[Received] {message} from {queue}");
            return message;
        }
    }
}
