using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Samples.KafkaBenchmark
{
    class Program
    {
        private const string BootstrapServers = "localhost:9092";
        private const string ConsumerGroupId = "benchmark-consumer-group";
        private const int MessageSize = 32;
        private static int ThreadCount;
        private const int MessageCount = 1000;
        private static string Topic;

        static async Task Main(string[] args)
        {
            Topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "benchmark-topic";
            ThreadCount = int.Parse(Environment.GetEnvironmentVariable("NUM_THREADS") ?? "5");

            try
            {
                Console.WriteLine($"Starting Kafka benchmark with {ThreadCount} threads...");

                var config = new ClientConfig
                {
                    BootstrapServers = BootstrapServers
                };

                await RunMultiThreadedBenchmark();

                Console.WriteLine("Benchmark completed successfully");
                Environment.Exit(0);
            }
            catch (KafkaException ex)
                when (
                    ex.Message.Contains("Failed while waiting for response from broker: Local: Timed out")
                  || ex.Message.Contains("Failed while waiting for controller: Local: Timed out"))
            {
                Console.WriteLine("Unexpected exception during execution " + ex);
                Console.WriteLine("Exiting with skip code (13)");
                Environment.Exit(13);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during benchmark execution: {ex}");
                Environment.Exit(1);
            }
        }

        private static async Task RunMultiThreadedBenchmark()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < ThreadCount; i++)
            {
                string topicName = $"{Topic}-{i}";
                tasks.Add(Task.Run(() => RunBenchmark(topicName)));
            }

            await Task.WhenAll(tasks);
        }

        private static void RunBenchmark(string topicName)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = BootstrapServers,
                EnableDeliveryReports = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 1,
                RetryBackoffMs = 100,
            };

            var uniqueConsumerGroupId = $"{ConsumerGroupId}-{DateTime.UtcNow.Ticks}";

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = BootstrapServers,
                GroupId = uniqueConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false,
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 3000
            };

            using var producer = new ProducerBuilder<long, string>(producerConfig)
                .SetKeySerializer(Serializers.Int64)
                .SetValueSerializer(Serializers.Utf8)
                .SetErrorHandler((_, e) => Console.WriteLine($"Producer Error: {e.Reason}."))
                .Build();

            using var consumer = new ConsumerBuilder<long, string>(consumerConfig)
                .SetKeyDeserializer(Deserializers.Int64)
                .SetValueDeserializer(Deserializers.Utf8)
                .SetErrorHandler((_, e) => Console.WriteLine($"Consumer Error: {e.Reason}."))
                .Build();

            consumer.Subscribe(topicName);
            Console.WriteLine($"Producing {MessageCount} messages on topic {topicName}...");

            var largeContent = new string('x', MessageSize);

            // Phase 1: Produce all messages
            for (int i = 0; i < MessageCount; i++)
            {
                var headers = new Headers();
                for (var j = 0; j < 5; j++)
                {
                    headers.Add("key" + j, Encoding.UTF8.GetBytes("value" + j));
                }

                var message = new Message<long, string>
                {
                    Key = DateTime.UtcNow.Ticks,
                    Value = i.ToString(),
                    Headers = headers
                };

                producer.Produce(topicName, message);
            }

            producer.Flush();
            Console.WriteLine($"Successfully produced {MessageCount} messages on topic {topicName}");

            // Phase 2: Consume all messages
            Console.WriteLine($"Consuming {MessageCount} messages from topic {topicName}...");
            var consumedCount = 0;

            for (int i = 0; i < MessageCount; i++)
            {
                // Consume the message synchronously
                var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));
                if (consumeResult != null)
                {
                    // Commit the offset
                    consumer.Commit(consumeResult);
                    consumedCount++;
                }
                else
                {
                    throw new Exception($"Failed to consume message {i} within timeout on topic {topicName}");
                }
            }

            Console.WriteLine($"Successfully consumed {consumedCount} messages from topic {topicName}");
        }
    }
}
