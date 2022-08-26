using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace Samples.Kafka
{
    internal class Consumer: ConsumerBase
    {
        private Consumer(ConsumerConfig config, string topic, string consumerName)
            : base(config, topic, consumerName)
        {
        }

        protected override void HandleMessage(ConsumeResult<string, string> consumeResult)
        {
            var kafkaMessage = consumeResult.Message;
            Console.WriteLine($"{ConsumerName}: Consuming {kafkaMessage.Key}, {consumeResult.TopicPartitionOffset}");

            var messageHeaders = kafkaMessage.Headers;
            SampleHelpers.ExtractScope(messageHeaders, GetValues, out var traceId, out var spanId);

            IEnumerable<string> GetValues(Headers headers, string name)
            {
                if (headers.TryGetLastBytes(name, out var bytes))
                {
                    try
                    {
                        return new[] { Encoding.UTF8.GetString(bytes) };
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }

                return Enumerable.Empty<string>();
            }

            if (traceId is 0 || spanId is 0)
            {
                // For kafka brokers < 0.11.0, we can't inject custom headers, so context will not be propagated
                var errorMessage = $"Error extracting trace context for {kafkaMessage.Key}, {consumeResult.TopicPartitionOffset}";
                Console.WriteLine(errorMessage);
            }
            else
            {
                Console.WriteLine($"Successfully extracted trace context from message: {traceId}, {spanId}");
            }


            if (string.IsNullOrEmpty(kafkaMessage.Value))
            {
                Console.WriteLine($"Received Tombstone for {kafkaMessage.Key}");
                Interlocked.Increment(ref TotalTombstones);
            }
            else
            {
                var sampleMessage = JsonConvert.DeserializeObject<SampleMessage>(kafkaMessage.Value);
                Console.WriteLine($"Received {(sampleMessage.IsProducedAsync ? "async" : "sync")}message for {kafkaMessage.Key}");
                if (sampleMessage.IsProducedAsync)
                {
                    Interlocked.Increment(ref TotalAsyncMessages);
                }
                else
                {
                    Interlocked.Increment(ref TotalSyncMessages);
                }
            }
        }

        public static Consumer Create(bool enableAutoCommit, string topic, string consumerName)
        {
            Console.WriteLine($"Creating consumer '{consumerName}' and subscribing to topic {topic}");

            var config = new ConsumerConfig
            {
                BootstrapServers = Config.KafkaBrokerHost,
                GroupId = "Samples.Kafka." + consumerName,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = enableAutoCommit,
            };
            return new Consumer(config, topic, consumerName);
        }
    }
}
