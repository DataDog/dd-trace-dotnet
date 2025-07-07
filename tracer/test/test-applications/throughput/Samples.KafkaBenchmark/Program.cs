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
        private const string Topic = "benchmark-topic";
        private const string ConsumerGroupId = "benchmark-consumer-group";
        private static int MessageCount = 10;
        private const int MessageSize = 32;

        static void Main(string[] args)
        {
            MessageCount = int.Parse(Environment.GetEnvironmentVariable("MESSAGE_COUNT") ?? "10");

            try
            {
                Console.WriteLine("Starting Kafka benchmark...");
                
                var config = new ClientConfig
                {
                    BootstrapServers = BootstrapServers
                };

                // Create topic if it doesn't exist
                CreateTopicIfNotExists(Topic, config).GetAwaiter().GetResult();

                // Run the benchmark
                RunBenchmark();

                Console.WriteLine("Benchmark completed successfully");
                Environment.Exit(0);
            }
            catch (KafkaException ex) 
                when(
                    ex.Message.Contains("Failed while waiting for response from broker: Local: Timed out") 
                  || ex.Message.Contains("Failed while waiting for controller: Local: Timed out"))
            {
                // If the brokers are too slow in responding, we can end up with timeouts
                // However, we can't just do retries, as that would change the number
                // of spans causing the tests to fail. As a workaround, we use the specific
                // (arbitrary) exit code 13 to indicate a faulty program, and skip the test
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

        private static async Task CreateTopicIfNotExists(string topicName, ClientConfig config)
        {
            using var adminClient = new AdminClientBuilder(config).Build();
            
            try
            {
                Console.WriteLine($"Creating topic {topicName}...");
                await adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                    new()
                    {
                        Name = topicName,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                });
                Console.WriteLine($"Topic {topicName} created successfully");
            }
            catch (CreateTopicsException ex)
            {
                if (ex.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                {
                    Console.WriteLine($"Topic {topicName} already exists, skipping creation");
                }
                else
                {
                    Console.WriteLine($"Error creating topic {topicName}: {ex.Results[0].Error.Reason}");
                    throw;
                }
            }
        }

        private static void RunBenchmark()
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = BootstrapServers,
                EnableDeliveryReports = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 100,
            };

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = BootstrapServers,
                GroupId = ConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false
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

            // Subscribe to the topic
            consumer.Subscribe(Topic);

            Console.WriteLine($"Producing and consuming {MessageCount} messages...");

            var largeContent = new string('x', MessageSize);
            var consumedCount = 0;

            // Produce and consume messages
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

                // Produce message
                producer.Produce(Topic, message);
                producer.Flush();

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
                    throw new Exception($"Failed to consume message {i} within timeout");
                }
            }

            Console.WriteLine($"Successfully produced and consumed {consumedCount} messages");
        }
    }
}
