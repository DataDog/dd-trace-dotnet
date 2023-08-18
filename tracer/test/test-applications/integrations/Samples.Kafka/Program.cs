using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace Samples.Kafka
{
    class Program
    {

        // based on https://github.com/confluentinc/examples/blob/6.1.1-post/clients/cloud/csharp/Program.cs
        static async Task<int> Main(string[] args)
        {
            try
            {
                var topic = args.Length > 0
                                ? args[0]
                                : "sample-topic";

                var config = Config.Create();

                await TopicHelpers.TryDeleteTopic(topic, config);

                await ConsumeAgainstNonExistentTopic(topic, config);

                await ConsumeAndProduceMessages(topic, config);

                Console.WriteLine($"Shut down complete");
                return 0;
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
                // We need to keep the catch very specific here, so that we don't accidentally
                // start skipping tests when we shouldn't be
                Console.WriteLine("Unexpected exception during execution " + ex);
                Console.WriteLine("Exiting with skip code (13)");
                return 13;
            }
        }

        private static async Task ConsumeAgainstNonExistentTopic(string topic, ClientConfig config)
        {
            using var consumer = Consumer.Create(enableAutoCommit: true, topic, consumerName: "FailingConsumer1");

            Console.WriteLine($"Manually consuming non-existent topic...");

            // On Kafka.Confluent 1.5.3 this will throw, so success will be false
            // That creates an exception Span
            // On other versions, this _won't_ throw, and _won't_ create a span
            var success = consumer.Consume(retries: 1, timeoutMilliSeconds: 300);
            Console.WriteLine($"Manual consume complete, success {success}");

            // Create the topic and try again
            await TopicHelpers.TryCreateTopic(topic, numPartitions: 3, replicationFactor: 1, config);

            Console.WriteLine($"Manually consuming topic...");

            // manually try and consume. Should _not_ generate any spans, as nothing to consume
            // but on 1.5.3 this may generate some error spans
            success = consumer.Consume(retries: 5, timeoutMilliSeconds: 300);

            Console.WriteLine($"Manual consume finished, success {success}");
        }

        private static async Task ConsumeAndProduceMessages(string topic, ClientConfig config)
        {
            var commitPeriod = 3;

            var cts = new CancellationTokenSource();

            using var consumer1 = Consumer.Create(enableAutoCommit: true, topic, consumerName: "AutoCommitConsumer1");
            using var consumer2 = Consumer.Create(enableAutoCommit: false, topic, consumerName: "ManualCommitConsumer2");

            Console.WriteLine("Starting consumers...");

            var consumeTask1 = Task.Run(() => consumer1.Consume(cts.Token));
            var consumeTask2 = Task.Run(() => consumer2.ConsumeWithExplicitCommit(commitEveryXMessages: commitPeriod, cts.Token));

            Console.WriteLine($"Producing messages");

            var messagesProduced = await ProduceMessages(topic, config);

            // Wait for all messages to be consumed
            // This assumes that the topic starts empty, and nothing else is producing to the topic
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                var syncCount = Volatile.Read(ref Consumer.TotalSyncMessages);
                var asyncCount = Volatile.Read(ref Consumer.TotalAsyncMessages);
                var tombstoneCount = Volatile.Read(ref Consumer.TotalTombstones);

                if (syncCount >= messagesProduced.SyncMessages
                 && asyncCount >= messagesProduced.AsyncMessages
                 && tombstoneCount >= messagesProduced.TombstoneMessages)
                {
                    Console.WriteLine($"All messages produced and consumed");
                    break;
                }

                if (DateTime.UtcNow > deadline)
                {
                    Console.WriteLine($"Exiting consumer: did not consume all messages syncCount {syncCount}, asyncCount {asyncCount}");
                    break;
                }


                await Task.Delay(1000);
            }

            cts.Cancel();
            Console.WriteLine($"Waiting for graceful exit...");

            await Task.WhenAny(
                Task.WhenAll(consumeTask1, consumeTask2),
                Task.Delay(TimeSpan.FromSeconds(5)));
        }

        private static async Task<MessagesProduced> ProduceMessages(string topic, ClientConfig config)
        {
            // produce messages sync and async
            const int numberOfMessagesPerProducer = 10;

            // Send valid messages
            Producer.Produce(topic, numberOfMessagesPerProducer, config, handleDelivery: false, isTombstone: false);
            Producer.Produce(topic, numberOfMessagesPerProducer, config, handleDelivery: true, isTombstone: false);
            await Producer.ProduceAsync(topic, numberOfMessagesPerProducer, config, isTombstone: false);

            // Send tombstone messages
            Producer.Produce(topic, numberOfMessagesPerProducer, config, handleDelivery: false, isTombstone: true);
            Producer.Produce(topic, numberOfMessagesPerProducer, config, handleDelivery: true, isTombstone: true);
            await Producer.ProduceAsync(topic, numberOfMessagesPerProducer, config, isTombstone: true);

            // try to produce invalid messages
            const string invalidTopic = "INVALID-TOPIC";
            // Producer.Produce(invalidTopic, 1, config, handleDelivery: false); // failure won't be logged, more of a pain to test
            Producer.Produce(invalidTopic, 1, config, handleDelivery: true, isTombstone: false); // failure should be logged by delivery handler

            try
            {
                await Producer.ProduceAsync(invalidTopic, 1, config, isTombstone: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error producing a message to an unknown topic (expected): {ex}");
            }

            return new MessagesProduced
            {
                SyncMessages = numberOfMessagesPerProducer * 2,
                AsyncMessages = numberOfMessagesPerProducer * 1,
                TombstoneMessages = numberOfMessagesPerProducer * 3,
            };
        }

        private struct MessagesProduced
        {
            public int SyncMessages;
            public int AsyncMessages;
            public int TombstoneMessages;
        }
    }
}
