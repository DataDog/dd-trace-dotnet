using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Samples.KafkaBenchmark
{
    class Program
    {
        private const string BootstrapServers = "localhost:9092";
        private const string ConsumerGroupId = "benchmark-consumer-group";
        private static int ThreadCount;
        private static int MessageCount;
        private static int TopicsPerThread;
        private static string Topic;

        static async Task Main(string[] args)
        {
            try
            {
                Topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "benchmark-topic";
                ThreadCount = GetPositiveSetting("NUM_THREADS", 5);
                MessageCount = GetPositiveSetting("NUM_MESSAGES", 1000);
                TopicsPerThread = GetPositiveSetting("TOPICS_PER_THREAD", 1);
                if (MessageCount < TopicsPerThread)
                {
                    throw new ArgumentException("NUM_MESSAGES must be at least TOPICS_PER_THREAD so every topic receives a message.");
                }

                if (args.Length == 1 && args[0] == "--setup")
                {
                    await CreateTopics();
                    return;
                }

                if (args.Length != 0)
                {
                    throw new ArgumentException("The only supported argument is --setup.");
                }

                Console.WriteLine($"Starting Kafka benchmark with {ThreadCount} threads, {TopicsPerThread} topics per thread and {MessageCount} messages per thread...");

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

        private static int GetPositiveSetting(string name, int defaultValue)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (value == null)
            {
                return defaultValue;
            }

            if (!int.TryParse(value, out var result) || result <= 0)
            {
                throw new ArgumentException($"{name} must be a positive integer.");
            }

            return result;
        }

        private static string[] GetTopics(int thread)
        {
            // Keep the original topic names for the default, single-topic workload.
            return Enumerable.Range(0, TopicsPerThread)
                             .Select(topic => TopicsPerThread == 1 ? $"{Topic}-{thread}" : $"{Topic}-{thread}-{topic}")
                             .ToArray();
        }

        private static async Task CreateTopics()
        {
            using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = BootstrapServers }).Build();
            var topics = Enumerable.Range(0, ThreadCount).SelectMany(GetTopics).ToArray();
            // Topic creation belongs outside the timed process. Batch requests to avoid
            // starting thousands of Kafka administration processes in the runner.
            for (var start = 0; start < topics.Length; start += 100)
            {
                var batch = topics.Skip(start).Take(100)
                                  .Select(name => new TopicSpecification { Name = name, NumPartitions = 1, ReplicationFactor = 1 });
                try
                {
                    await admin.CreateTopicsAsync(batch);
                }
                catch (CreateTopicsException ex) when (ex.Results.All(result => result.Error.Code == ErrorCode.NoError || result.Error.Code == ErrorCode.TopicAlreadyExists))
                {
                    // Warmups and later experiments can reuse topics created by an earlier setup.
                }
            }

            Console.WriteLine($"Prepared {topics.Length} topics.");
        }

        private static async Task RunMultiThreadedBenchmark()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < ThreadCount; i++)
            {
                var topics = GetTopics(i);
                tasks.Add(Task.Run(() => RunBenchmark(topics)));
            }

            await Task.WhenAll(tasks);
        }

        private static void RunBenchmark(string[] topics)
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

            if (TopicsPerThread == 1)
            {
                consumer.Subscribe(topics[0]);
            }

            var firstOffsets = new ConcurrentDictionary<string, TopicPartitionOffset>();
            Action<DeliveryReport<long, string>> recordFirstOffset = report =>
            {
                if (!report.Error.IsError)
                {
                    firstOffsets.TryAdd(report.Topic, report.TopicPartitionOffset);
                }
            };
            Console.WriteLine($"Producing {MessageCount} messages across {topics.Length} topics...");

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

                // Revisit the topics in a cycle so a cache smaller than the working set
                // must repeatedly handle uncached tags rather than only a one-time fill.
                producer.Produce(topics[i % topics.Length], message, TopicsPerThread > 1 && i < topics.Length ? recordFirstOffset : null);
            }

            producer.Flush();
            Console.WriteLine($"Successfully produced {MessageCount} messages across {topics.Length} topics");

            if (TopicsPerThread > 1)
            {
                if (firstOffsets.Count != topics.Length)
                {
                    throw new Exception("Failed to deliver the first message to every topic.");
                }

                // Each topic has one partition. Start at this run's first produced offset,
                // otherwise repeated measurements could consume old messages from only a
                // subset of topics and never exercise the full consumer tag working set.
                consumer.Assign(topics.Select(topic => firstOffsets[topic]));
            }

            // Phase 2: Consume all messages
            Console.WriteLine($"Consuming {MessageCount} messages across {topics.Length} topics...");
            var consumedCount = 0;
            var consumedPerTopic = new Dictionary<string, int>();

            for (int i = 0; i < MessageCount; i++)
            {
                // Consume the message synchronously
                var consumeResult = consumer.Consume(TimeSpan.FromSeconds(10));
                if (consumeResult != null)
                {
                    // Commit the offset
                    consumer.Commit(consumeResult);
                    consumedCount++;
                    if (TopicsPerThread > 1)
                    {
                        consumedPerTopic.TryGetValue(consumeResult.Topic, out var count);
                        consumedPerTopic[consumeResult.Topic] = count + 1;
                    }
                }
                else
                {
                    throw new Exception($"Failed to consume message {i} within timeout");
                }
            }

            if (TopicsPerThread > 1)
            {
                for (var i = 0; i < topics.Length; i++)
                {
                    var expected = (MessageCount / topics.Length) + (i < MessageCount % topics.Length ? 1 : 0);
                    if (!consumedPerTopic.TryGetValue(topics[i], out var actual) || actual != expected)
                    {
                        throw new Exception($"Expected {expected} messages from {topics[i]}, consumed {actual}.");
                    }
                }
            }

            Console.WriteLine($"Successfully consumed {consumedCount} messages across {topics.Length} topics");
        }
    }
}
