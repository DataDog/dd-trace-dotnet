using System;
using System.IO;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.InteropServices;
#endif
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
        if (nativeVersion != "2.8.0")
        {
            var libraryName = "librdkafka.dll";
#if NETCOREAPP3_1_OR_GREATER
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libraryName = File.Exists("/etc/alpine-release") ? "alpine-librdkafka.so" : "centos6-librdkafka.so";
            }
#endif
            var libraryPath = Path.Combine(AppContext.BaseDirectory, $"librdkafka-{nativeVersion}", libraryName);
#if NETCOREAPP3_1_OR_GREATER
            // Library.Load(path) alone does not override .NET's resolution of Confluent's P/Invokes.
            // Keep the handle alive for the process, including native background threads and cleanup.
            var loadFlags = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                                ? DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories
                                : (DllImportSearchPath?)null;
            var nativeHandle = NativeLibrary.Load(libraryPath, typeof(Library).Assembly, loadFlags);
            NativeLibrary.SetDllImportResolver(typeof(Library).Assembly, (name, assembly, searchPath) =>
                name is "librdkafka" or "alpine-librdkafka" ? nativeHandle : IntPtr.Zero);
            // The resolver also handles Confluent's Alpine binding, without requiring its explicit-path libdl loader.
            Library.Load();
#else
            Library.Load(libraryPath);
#endif
        }

        var expectedVersion = nativeVersion switch
        {
            "1.6.1" => 0x010601ff,
            "2.2.0" => 0x020200ff,
            "2.8.0" => 0x020800ff,
            _ => throw new ArgumentException("Unexpected native version"),
        };
        if (Library.Version != expectedVersion)
        {
            throw new InvalidOperationException($"Expected native version {expectedVersion:x8}, loaded {Library.Version:x8}");
        }

        Console.WriteLine($"Native version: {Library.VersionString}");
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
        using var firstClient = producerFirst ? (IDisposable)producerBuilder.Build() : consumerBuilder.Build();
        using var secondClient = producerFirst ? (IDisposable)consumerBuilder.Build() : producerBuilder.Build();
        var producer = (IProducer<string, string>)(producerFirst ? firstClient : secondClient);
        var consumer = (IConsumer<string, string>)(producerFirst ? secondClient : firstClient);
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
