using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace Samples.Kafka.LibraryMismatch;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var nativeVersion = args[0];
        var producerFirst = bool.Parse(args[1]);
        var topic = args[2];

        // This must precede all Kafka operations, including the first version query.
        NativeLibraryLoader.LoadAndVerify(nativeVersion);

        var bootstrapServers = Environment.GetEnvironmentVariable("KAFKA_BROKER_HOST") ?? "localhost:9092";
        var producerBuilder = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            MessageTimeoutMs = 30000,
        });
        var consumerBuilder = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = topic,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        });

        // Each test runs in a fresh process so either constructor can exercise the first lookup.
        if (producerFirst)
        {
            using var producer = producerBuilder.Build();
            using var consumer = consumerBuilder.Build();
            await RunMessagingScenarioAsync(producer, consumer, bootstrapServers, topic, nativeVersion);
        }
        else
        {
            using var consumer = consumerBuilder.Build();
            using var producer = producerBuilder.Build();
            await RunMessagingScenarioAsync(producer, consumer, bootstrapServers, topic, nativeVersion);
        }
    }

    private static async Task RunMessagingScenarioAsync(
        IProducer<string, string> producer,
        IConsumer<string, string> consumer,
        string bootstrapServers,
        string topic,
        string nativeVersion)
    {
        Console.WriteLine("Both clients constructed");

        using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();
        await adminClient.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
        try
        {
            await producer.ProduceAsync(topic, new Message<string, string> { Key = "key", Value = "value" });
            Console.WriteLine("Message produced");
            // Confluent.Kafka 2.8.0's Consume() requires leader-epoch exports absent in 1.6.1,
            // independently of instrumentation. That pairing covers construction and production.
            if (nativeVersion == "1.6.1")
            {
                return;
            }

            consumer.Subscribe(topic);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var consumed = consumer.Consume(timeout.Token);
            if (consumed.Message.Key != "key" || consumed.Message.Value != "value")
            {
                throw new InvalidOperationException("The consumed message did not match the produced message");
            }

            consumer.Commit(consumed);
            consumer.Close();
            Console.WriteLine("Message produced, consumed, and committed");
        }
        finally
        {
            await adminClient.DeleteTopicsAsync([topic]);
        }
    }
}
