using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace Samples.Kafka
{
    internal class Consumer: IDisposable
    {
        private readonly string _consumerName;
        private readonly IConsumer<string, string> _consumer;

        public static int TotalAsyncMessages = 0;
        public static int TotalSyncMessages = 0;
        public static int TotalTombstones = 0;

        private Consumer(ConsumerConfig config, string topic, string consumerName)
        {
            _consumerName = consumerName;
            _consumer = new ConsumerBuilder<string, string>(config).Build();
            _consumer.Subscribe(topic);
        }


        public bool Consume(int retries, int timeoutMilliSeconds)
        {
            try
            {
                for (int i = 0; i < retries; i++)
                {
                    try
                    {
                        // will block until a message is available
                        // on 1.5.3 this will throw if the topic doesn't exist
                        var consumeResult = _consumer.Consume(timeoutMilliSeconds);
                        if (consumeResult is null)
                        {
                            Console.WriteLine($"{_consumerName}: Null consume result");
                            return true;
                        }

                        if (consumeResult.IsPartitionEOF)
                        {
                            Console.WriteLine($"{_consumerName}: Reached EOF");
                            return true;
                        }
                        else
                        {
                            HandleMessage(consumeResult);
                            return true;
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Consume Exception in manual consume: {ex}");
                    }

                    Task.Delay(500);
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{_consumerName}: Cancellation requested, exiting.");
            }

            return false;
        }

        public void Consume(CancellationToken cancellationToken = default)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // will block until a message is available
                    var consumeResult = _consumer.Consume(cancellationToken);

                    if (consumeResult.IsPartitionEOF)
                    {
                        Console.WriteLine($"{_consumerName}: Reached EOF");
                    }
                    else
                    {
                        HandleMessage(consumeResult);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{_consumerName}: Cancellation requested, exiting.");
            }
        }

        public void ConsumeWithExplicitCommit(int commitEveryXMessages, CancellationToken cancellationToken = default)
        {
            ConsumeResult<string, string> consumeResult = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // will block until a message is available
                    consumeResult = _consumer.Consume(cancellationToken);

                    if (consumeResult.IsPartitionEOF)
                    {
                        Console.WriteLine($"{_consumerName}: Reached EOF");
                    }
                    else
                    {
                        HandleMessage(consumeResult);
                    }

                    if (consumeResult.Offset % commitEveryXMessages == 0)
                    {
                        try
                        {
                            Console.WriteLine($"{_consumerName}: committing...");
                            _consumer.Commit(consumeResult);
                        }
                        catch (KafkaException e)
                        {
                            Console.WriteLine($"{_consumerName}: commit error: {e.Error.Reason}");
                        }
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"{_consumerName}: Cancellation requested, exiting.");
            }

            // As we're doing manual commit, make sure we force a commit now
            if (consumeResult is not null)
            {
                Console.WriteLine($"{_consumerName}: committing...");
                _consumer.Commit(consumeResult);
            }
        }

        private void HandleMessage(ConsumeResult<string, string> consumeResult)
        {
            var kafkaMessage = consumeResult.Message;
            Console.WriteLine($"{_consumerName}: Consuming {kafkaMessage.Key}, {consumeResult.TopicPartitionOffset}");

            var headers = kafkaMessage.Headers;
            ulong? traceId = headers.TryGetLastBytes("x-datadog-trace-id", out var traceIdBytes)
                          && ulong.TryParse(Encoding.UTF8.GetString(traceIdBytes), out var extractedTraceId)
                                 ? extractedTraceId
                                 : null;

            ulong? parentId = headers.TryGetLastBytes("x-datadog-parent-id", out var parentBytes)
                           && ulong.TryParse(Encoding.UTF8.GetString(parentBytes), out var extractedParentId)
                                  ? extractedParentId
                                  : null;

            if (traceId is null || parentId is null)
            {
                // For kafka brokers < 0.11.0, we can't inject custom headers, so context will not be propagated
                var errorMessage = $"Error extracting trace context for {kafkaMessage.Key}, {consumeResult.TopicPartitionOffset}";
                Console.WriteLine(errorMessage);
            }
            else
            {
                Console.WriteLine($"Successfully extracted trace context from message: {traceId}, {parentId}");
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

        public void Dispose()
        {
            Console.WriteLine($"{_consumerName}: Closing consumer");
            _consumer?.Close();
            _consumer?.Dispose();
        }

        public static Consumer Create(bool enableAutoCommit, string topic, string consumerName)
        {
            Console.WriteLine($"Creating consumer '{consumerName}' and subscribing to topic {topic}");

            var config = new ConsumerConfig
            {
                BootstrapServers = Config.KafkaBrokerHost,
                GroupId = "Samples.Kafka.TestConsumer",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = enableAutoCommit,
            };
            return new Consumer(config, topic, consumerName);
        }
    }
}
