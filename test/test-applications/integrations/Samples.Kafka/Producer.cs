using System;
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
        private static readonly TimeSpan FlushTimeout = TimeSpan.FromSeconds(5);
        private static int _messageNumber = 0;

        public static async Task ProduceAsync(string topic, int numMessages, ClientConfig config, bool isTombstone)
        {
            using (var producer = new ProducerBuilder<string, string>(config).Build())
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
                        var deliveryResult = await producer.ProduceAsync(topic, message);
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

        public static void Produce(string topic, int numMessages, ClientConfig config, bool handleDelivery, bool isTombstone)
        {
            Produce(topic, numMessages, config, handleDelivery ? HandleDelivery : null, isTombstone);
        }

        private static void Produce(string topic, int numMessages, ClientConfig config, Action<DeliveryReport<string, string>> deliveryHandler, bool isTombstone)
        {
            using (var producer = new ProducerBuilder<string, string>(config).Build())
            {
                for (var i=0; i<numMessages; ++i)
                {
                    var messageNumber = Interlocked.Increment(ref _messageNumber);
                    var hasHandler = deliveryHandler is not null;
                    var key = $"{messageNumber}-Sync-{hasHandler}{(isTombstone ? "-tombstone" : "")}";
                    var value = isTombstone ? null : GetMessage(i, isProducedAsync: false);
                    var message = new Message<string, string> { Key = key, Value = value };

                    Console.WriteLine($"Producing record {i}: {message.Key}...");

                    producer.Produce(topic, message, deliveryHandler);

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
