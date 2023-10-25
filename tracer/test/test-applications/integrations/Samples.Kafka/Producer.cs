using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Samples.Kafka
{
    internal static class Producer
    {
        // Flush every x messages
        private const int FlushInterval = 3;
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(1);
        private static readonly ConcurrentDictionary<string, int> TopicPartitions = new ();
        private static readonly ConcurrentDictionary<string, int> LastUsedPartition = new();
        private static int _messageNumber = 0;

        public static async Task ProduceAsync(string topic, int numMessages, ClientConfig config, bool isTombstone, bool explicitPartitions = false)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = config.BootstrapServers,
                MessageTimeoutMs = 3000 // earlier versions would return right away when producing to invalid topics - later versions would block for 30 seconds
            };
            
            using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
            {
                for (var i=0; i<numMessages; ++i)
                {
                    var messageNumber = Interlocked.Increment(ref _messageNumber);
                    var key = $"{messageNumber}-Async{(isTombstone ? "-tombstone" : "")}";
                    var value = isTombstone ? null : GetMessage(i, isProducedAsync: true);
                    var message = new Message<string, string> { Key = key, Value = value };

                    Console.WriteLine($"Producing record {i}: {key}...");

                    try
                    {
                        DeliveryResult<string, string> deliveryResult;
                        if (explicitPartitions)
                        {
                            deliveryResult = await producer.ProduceAsync(new TopicPartition(topic, GetPartition(config, topic)), message);
                        }
                        else
                        {
                            deliveryResult = await producer.ProduceAsync(topic, message);
                        }
                        
                        Console.WriteLine($"Produced message to: {deliveryResult.TopicPartitionOffset}");
                    }
                    catch (ProduceException<string, string> ex)
                    {
                        Console.WriteLine($"Failed to deliver message: {ex.Error.Reason}");
                    }
                }

                Flush(producer);
                Console.WriteLine($"Finished producing {numMessages} messages to topic {topic}");
            }
        }

        private static void Flush(IProducer<string, string> producer)
        {
            var queueLength = 1;
            while (queueLength > 0)
            {
                queueLength = producer.Flush(FlushTimeout);
            }
        }
        
        private static int GetTopicPartitionCount(string topic, ClientConfig config)
        {
            var partitions = new HashSet<int>();
            using var aminClient = new AdminClientBuilder(config).Build();
            
            var metadata = aminClient.GetMetadata(topic, TimeSpan.FromSeconds(5));
            var topicMetadata = metadata.Topics.FirstOrDefault(w => w.Topic == topic);
            if (topicMetadata != null)
            {
                return topicMetadata.Partitions.Count;
            }

            return 0;
        }

        private static Partition GetPartition(ClientConfig config, string topic)
        {
            var numPartitions = TopicPartitions.GetOrAdd(topic, t => GetTopicPartitionCount(topic, config));
            var partition = LastUsedPartition.GetOrAdd(topic, t => 0);
            if (partition >= numPartitions)
            {
                partition = 0;
            }

            LastUsedPartition.TryUpdate(topic, partition + 1, partition);
            return new Partition(partition);
        }

        public static void Produce(string topic, int numMessages, ClientConfig config, bool handleDelivery, bool isTombstone, bool explicitPartitions = false)
        {
            Produce(topic, numMessages, config, handleDelivery ? HandleDelivery : null, isTombstone, explicitPartitions);
        }

        private static void Produce(string topic, int numMessages, ClientConfig config, Action<DeliveryReport<string, string>> deliveryHandler, bool isTombstone, bool explicitPartitions = false)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = config.BootstrapServers,
                MessageTimeoutMs = 3000 // earlier versions would return right away when producing to invalid topics - later versions would block for 30 seconds
            };

            using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
            {
                for (var i=0; i<numMessages; ++i)
                {
                    var messageNumber = Interlocked.Increment(ref _messageNumber);
                    var hasHandler = deliveryHandler is not null;
                    var key = $"{messageNumber}-Sync-{hasHandler}{(isTombstone ? "-tombstone" : "")}";
                    var value = isTombstone ? null : GetMessage(i, isProducedAsync: false);
                    var message = new Message<string, string> { Key = key, Value = value };

                    Console.WriteLine($"Producing record {i}: {message.Key}...");
                    if (explicitPartitions)
                    {
                        producer.Produce(new TopicPartition(topic, GetPartition(config, topic)), message, deliveryHandler); 
                    }
                    else
                    {
                        producer.Produce(topic, message, deliveryHandler);
                    }

                    if (numMessages % FlushInterval == 0)
                    {
                        producer.Flush(FlushTimeout);
                    }
                }
                Flush(producer);

                Console.WriteLine($"Finished producing {numMessages} messages to topic {topic}");
            }
        }

        private static void HandleDelivery(DeliveryReport<string, string> deliveryReport)
        {
            if (deliveryReport.Error.Code != ErrorCode.NoError)
            {
                Console.WriteLine($"Failed to deliver message: {deliveryReport.Error.Reason}");
            }
            else
            {
                Console.WriteLine($"Produced message to: {deliveryReport.TopicPartitionOffset}");
            }
        }

        static string GetMessage(int iteration, bool isProducedAsync)
        {
            var message = new SampleMessage("fruit", iteration, isProducedAsync);
            return JObject.FromObject(message).ToString(Formatting.None);
        }
    }

    public class SampleMessage
    {
        public string Category { get; }
        public int MessageNumber { get; }
        public bool IsProducedAsync { get; }

        public SampleMessage(string category, int messageNumber, bool isProducedAsync)
        {
            Category = category;
            MessageNumber = messageNumber;
            IsProducedAsync = isProducedAsync;
        }
    }
}
