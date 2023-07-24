using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using RabbitMQ.Client;

namespace Samples.DataStreams.RabbitMQ
{
    public static class Program
    {
        private static readonly string DefaultQueue = nameof(DefaultQueue);
        private static readonly string DirectQueue1 = nameof(DirectQueue1);
        private static readonly string FanoutQueue1 = nameof(FanoutQueue1);
        private static readonly string FanoutQueue2 = nameof(FanoutQueue2);
        private static readonly string FanoutQueue3 = nameof(FanoutQueue3);
        private static readonly string TopicQueue1 = nameof(TopicQueue1);
        private static readonly string TopicQueue2 = nameof(TopicQueue2);
        private static readonly string TopicQueue3 = nameof(TopicQueue3);
        private static readonly string DirectExchange = nameof(DirectExchange);
        private static readonly string TopicExchange = nameof(TopicExchange);
        private static readonly string FanoutExchange = nameof(FanoutExchange);
        private static readonly string DirectRoutingKey = nameof(DirectRoutingKey);
        private static readonly string Message = nameof(Message);
        private static readonly string Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        
        public static async Task Main(string[] args)
        {
            await SampleHelpers.WaitForDiscoveryService();

            var factory = new ConnectionFactory() { HostName = Host };
            using var connection = factory.CreateConnection();
            using var model = connection.CreateModel();

            // This should create 4 separate Rabbit Pipeline:
            // 1. default exchange pipeline:
            //    (direction:out, topic:DefaultQueue) -> (direction:in, topic:DefaultQueue)
            // 2. direct exchange pipeline:
            //    (direction:out, exchange:DirectExchange, has_routing_key:True) ->
            //    (direction:in, topic:DirectQueue1)
            // 3. fanout exchange pipeline (fanning out into 3 consumers)
            //    (direction:out, exchange:FanoutExchange, has_routing_key:False) ->
            //        (direction:in, topic:FanoutQueue1)
            //        (direction:in, topic:FanoutQueue2)
            //        (direction:in, topic:FanoutQueue3)
            // 4. topic exchange -- 2 separate publishes resulting in pipelines as follows:
            //    (direction:out, exchange:TopicExchange, has_routing_key:True) ->
            //        (direction:in, topic:FanoutQueue1)
            //        (direction:in, topic:FanoutQueue3)
            //    (direction:out, exchange:TopicExchange, has_routing_key:True) ->
            //        (direction:in, topic:FanoutQueue2)

            // produce (default exchange)
            PublishMessageToDefaultExchange(model);

            // produce (direct exchange)
            List<string> directQueues = PublishMessageToDirectExchange(model);

            // produce (fanout exchange)
            List<string> fanoutQueues = PublishMessageToFanoutExchange(model);

            // produce (topic exchange)
            List<string> topicQueues = PublishMessageToTopicExchange(model);

            GetMessage(model, DefaultQueue);
            foreach (var queue in directQueues) {
                GetMessage(model, queue);
            }
            foreach (var queue in topicQueues) {
                GetMessage(model, queue);
            }
            foreach (var queue in fanoutQueues) {
                GetMessage(model, queue);
            }
        }

        private static void PublishMessageToDefaultExchange(IModel model)
        {
            // Atomically binded to default exchange with routing key of the same name
            model.QueueDeclare(queue: DefaultQueue,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueuePurge(DefaultQueue);

            model.BasicPublish(
                // no exchange, aka default
                "",
                // routing key === queue name
                DefaultQueue,
                null, 
                Encoding.UTF8.GetBytes(Message));
            
            Console.WriteLine($"[Sent] {Message} to {DefaultQueue} in default exchange.");
        }

        private static List<string> PublishMessageToDirectExchange(IModel model)
        {
            model.ExchangeDeclare(DirectExchange, "direct");
            model.QueueDeclare(queue: DirectQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueBind(DirectQueue1, DirectExchange, DirectRoutingKey);
            model.QueuePurge(DirectQueue1);

            model.BasicPublish(exchange: DirectExchange,
                               routingKey: DirectRoutingKey,
                               basicProperties: null,
                               body: Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {DirectExchange}, using routing key {DirectRoutingKey}.");
            List<string> output = new List<string>();
            output.Add(DirectQueue1);
            return output;
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

        private static List<string> PublishMessageToTopicExchange(IModel model)
        {
            model.ExchangeDeclare(TopicExchange, "topic");
            model.QueueDeclare(queue: TopicQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueDeclare(queue: TopicQueue2,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueDeclare(queue: TopicQueue3,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            model.QueueBind(TopicQueue1, TopicExchange, "test.topic.*.cake");
            model.QueueBind(TopicQueue2, TopicExchange, "test.topic.vanilla.*");
            model.QueueBind(TopicQueue3, TopicExchange, "test.topic.chocolate.*");
            model.QueuePurge(TopicQueue1);
            model.QueuePurge(TopicQueue2);
            model.QueuePurge(TopicQueue3);

            // Routes to queue1 and queue3.
            model.BasicPublish(exchange: TopicExchange,
                               routingKey: "test.topic.chocolate.cake",
                               basicProperties: null,
                               body: Encoding.UTF8.GetBytes(Message));
            // Routes to queue2.
            model.BasicPublish(exchange: TopicExchange,
                               routingKey: "test.topic.vanilla.icecream",
                               basicProperties: null,
                               body: Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {TopicExchange}, a Topic exchange.");
            List<string> output = new List<string>();
            output.Add(TopicQueue1);
            output.Add(TopicQueue2);
            output.Add(TopicQueue3);
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
