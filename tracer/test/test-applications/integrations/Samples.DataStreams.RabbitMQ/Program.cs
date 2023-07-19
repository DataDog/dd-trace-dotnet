using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Samples.DataStreams.RabbitMQ
{
    public static class Program
    {
        private static readonly string DirectQueue = nameof(DirectQueue);
        private static readonly string FanoutQueue1 = nameof(FanoutQueue1);
        private static readonly string FanoutQueue2 = nameof(FanoutQueue2);
        private static readonly string FanoutQueue3 = nameof(FanoutQueue3);
        private static readonly string DirectExchange = nameof(DirectExchange);
        private static readonly string TopicExchange = nameof(TopicExchange);
        private static readonly string FanoutExchange = nameof(FanoutExchange);
        private static readonly string RoutingKey = nameof(RoutingKey);
        private static readonly string Message = nameof(Message);
        private static readonly string Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        
        public static async Task Main(string[] args)
        {
#if RABBITMQ_6_5_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_5_0");
#endif
#if RABBITMQ_6_4_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_4_0");
#endif
#if RABBITMQ_6_3_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_3_0");
#endif
#if RABBITMQ_6_2_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_2_0");
#endif
#if RABBITMQ_6_1_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_1_0");
#endif
#if RABBITMQ_6_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_6_0");
#endif
#if RABBITMQ_5_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_5_0");
#endif
#if RABBITMQ_4_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_4_0");
#endif
#if RABBITMQ_3_0
            Console.WriteLine("RABBIT_VERSION IS: RABBITMQ_3_0");
#endif
            await SampleHelpers.WaitForDiscoveryService();

            var factory = new ConnectionFactory() { HostName = Host };
            using var connection = factory.CreateConnection();
            using var model = connection.CreateModel();
        
            // produce/consume operation (direct exchange)
            //PublishMessageToQueue(model, Queue, Message);
            //var msg = GetMessage(model, Queue);

            // produce/consume operation (fanout exchange)
            List<string> fanoutQueues = PublishMessageToFanoutExchange(model);
            foreach (var queue in fanoutQueues) {
                GetMessage(model, queue);
            }

            // continuing the chain
            //var queueName = PublishMessageToExchange(model, Exchange, RoutingKey, msg);
            //GetMessage(model, queueName);
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

        private static List<string> PublishMessageToFanoutExchange(IModel model)
        {
            model.ExchangeDeclare(FanoutExchange, "fanout");
            model.QueueDeclare(queue: FanoutQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueDeclare(queue: FanoutQueue2,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueDeclare(queue: FanoutQueue3,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueBind(FanoutQueue1, FanoutExchange, "");
            model.QueueBind(FanoutQueue2, FanoutExchange, "");
            model.QueueBind(FanoutQueue3, FanoutExchange, "");
            model.QueuePurge(FanoutQueue1);
            model.QueuePurge(FanoutQueue2);
            model.QueuePurge(FanoutQueue3);

            model.BasicPublish(exchange: FanoutExchange,
                               routingKey: string.Empty,
                               basicProperties: null,
                               body: Encoding.UTF8.GetBytes(Message));
            
            Console.WriteLine($"[Sent] {Message} to {FanoutExchange}, a Fanout exchange.");
            List<string> output = new List<string>();
            output.Add(FanoutQueue1);
            output.Add(FanoutQueue2);
            output.Add(FanoutQueue3);
            return output;
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
