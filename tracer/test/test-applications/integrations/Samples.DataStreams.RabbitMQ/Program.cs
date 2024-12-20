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
#if RABBITMQ_7_0
            using var connection = await factory.CreateConnectionAsync();
            using var model = await connection.CreateChannelAsync();
#else
            using var connection = factory.CreateConnection();
            using var model = connection.CreateModel();
#endif

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
            await PublishMessageToDefaultExchange(model);

            // produce (direct exchange)
            var directQueues = await PublishMessageToDirectExchange(model);

            // produce (fanout exchange)
            var fanoutQueues = await PublishMessageToFanoutExchange(model);

            // produce (topic exchange)
            var topicQueues = await PublishMessageToTopicExchange(model);

            await GetMessage(model, DefaultQueue);
            foreach (var queue in directQueues) {
                await GetMessage(model, queue);
            }
            foreach (var queue in topicQueues) {
                await GetMessage(model, queue);
            }
            foreach (var queue in fanoutQueues) {
                await GetMessage(model, queue);
            }
        }

#if RABBITMQ_7_0
        private static async Task PublishMessageToDefaultExchange(IChannel model)
        {
            // Atomically binded to default exchange with routing key of the same name
            await model.QueueDeclareAsync(queue: DefaultQueue,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueuePurgeAsync(DefaultQueue);

            await model.BasicPublishAsync(
                // no exchange, aka default
                "",
                // routing key === queue name
                DefaultQueue,
                Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {DefaultQueue} in default exchange.");
        }

        private static async Task<List<string>> PublishMessageToDirectExchange(IChannel model)
        {
            await model.ExchangeDeclareAsync(DirectExchange, "direct");
            await model.QueueDeclareAsync(queue: DirectQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueBindAsync(DirectQueue1, DirectExchange, DirectRoutingKey);
            await model.QueuePurgeAsync(DirectQueue1);

            await model.BasicPublishAsync(exchange: DirectExchange,
                               routingKey: DirectRoutingKey,
                               body: Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {DirectExchange}, using routing key {DirectRoutingKey}.");
            List<string> output = new List<string>();
            output.Add(DirectQueue1);
            return output;
        }

        private static async Task<List<string>> PublishMessageToFanoutExchange(IChannel model)
        {
            await model.ExchangeDeclareAsync(FanoutExchange, "fanout");
            await model.QueueDeclareAsync(queue: FanoutQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueDeclareAsync(queue: FanoutQueue2,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueDeclareAsync(queue: FanoutQueue3,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueBindAsync(FanoutQueue1, FanoutExchange, "");
            await model.QueueBindAsync(FanoutQueue2, FanoutExchange, "");
            await model.QueueBindAsync(FanoutQueue3, FanoutExchange, "");
            await model.QueuePurgeAsync(FanoutQueue1);
            await model.QueuePurgeAsync(FanoutQueue2);
            await model.QueuePurgeAsync(FanoutQueue3);

            await model.BasicPublishAsync(exchange: FanoutExchange,
                               routingKey: string.Empty,
                               body: Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {FanoutExchange}, a Fanout exchange.");
            List<string> output = new List<string>();
            output.Add(FanoutQueue1);
            output.Add(FanoutQueue2);
            output.Add(FanoutQueue3);
            return output;
        }

        private static async Task<List<string>> PublishMessageToTopicExchange(IChannel model)
        {
            await model.ExchangeDeclareAsync(TopicExchange, "topic");
            await model.QueueDeclareAsync(queue: TopicQueue1,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueDeclareAsync(queue: TopicQueue2,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueDeclareAsync(queue: TopicQueue3,
                               durable: false,
                               exclusive: false,
                               autoDelete: false,
                               arguments: null);
            await model.QueueBindAsync(TopicQueue1, TopicExchange, "test.topic.*.cake");
            await model.QueueBindAsync(TopicQueue2, TopicExchange, "test.topic.vanilla.*");
            await model.QueueBindAsync(TopicQueue3, TopicExchange, "test.topic.chocolate.*");
            await model.QueuePurgeAsync(TopicQueue1);
            await model.QueuePurgeAsync(TopicQueue2);
            await model.QueuePurgeAsync(TopicQueue3);

            // Routes to queue1 and queue3.
            await model.BasicPublishAsync(exchange: TopicExchange,
                               routingKey: "test.topic.chocolate.cake",
                               body: Encoding.UTF8.GetBytes(Message));
            // Routes to queue2.
            await model.BasicPublishAsync(exchange: TopicExchange,
                               routingKey: "test.topic.vanilla.icecream",
                               body: Encoding.UTF8.GetBytes(Message));

            Console.WriteLine($"[Sent] {Message} to {TopicExchange}, a Topic exchange.");
            List<string> output = new List<string>();
            output.Add(TopicQueue1);
            output.Add(TopicQueue2);
            output.Add(TopicQueue3);
            return output;
        }

        private static async Task<string> GetMessage(IChannel model, string queue)
        {
            var result = await model.BasicGetAsync(queue, true);
#if RABBITMQ_6_0
            var message = Encoding.UTF8.GetString(result.Body.ToArray());
#else
            var message =  Encoding.UTF8.GetString(result.Body);
#endif
            Console.WriteLine($"[Received] {message} from {queue}");
            return message;
        }
#else
        private static Task PublishMessageToDefaultExchange(IModel model)
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
            return Task.CompletedTask;
        }

        private static Task<List<string>> PublishMessageToDirectExchange(IModel model)
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
            return Task.FromResult(output);
        }

        private static Task<List<string>> PublishMessageToFanoutExchange(IModel model)
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
            return Task.FromResult(output);
        }

        private static Task<List<string>> PublishMessageToTopicExchange(IModel model)
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
            return Task.FromResult(output);
        }

        private static Task<string> GetMessage(IModel model, string queue)
        {
            var result = model.BasicGet(queue, true);
#if RABBITMQ_6_0
            var message = Encoding.UTF8.GetString(result.Body.ToArray());
#else
            var message =  Encoding.UTF8.GetString(result.Body);
#endif
            Console.WriteLine($"[Received] {message} from {queue}");
            return Task.FromResult(message);
        }
#endif
    }
}
