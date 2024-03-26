using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using Samples;
using Samples.DataStreams.Kafka;
using Samples.Kafka;
using Config = Samples.Kafka.Config;

var sw = new Stopwatch();
sw.Start();

var runBatchProcessing = args.Contains("--batch-processing");
var extractScopesManually = Environment.GetEnvironmentVariable("DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED") == "0";

await (runBatchProcessing ? RunBatchProcessingScenario() : RunStandardPipelineScenario());

async Task RunStandardPipelineScenario()
{
    // Create Topics
    var topicPrefix = "data-streams";

    var topic1 = $"{topicPrefix}-1";
    var topic2 = $"{topicPrefix}-2";
    var topic3 = $"{topicPrefix}-3";
    var allTopics = new[] { topic1, topic2, topic3 };
    var topic3ConsumeCount = 0;

    var config = Config.Create();
    Console.WriteLine("Creating topics...");
    foreach (var topic in allTopics)
    {
        await TopicHelpers.TryDeleteTopic(topic, config);
        await TopicHelpers.TryCreateTopic(topic, numPartitions: 3, replicationFactor: 1, config);
    }

    LogWithTime("Finished creating topics");

    Console.WriteLine($"Creating consumers...");
    var consumer1 = Consumer.Create(topic1, "consumer-1", HandleAndProduceToTopic2);
    var consumer2 = Consumer.Create(topic2, "consumer-2", HandleAndProduceToTopic3, false);
    var consumer3 = Consumer.Create(topic3, "consumer-3", HandleTopic3, false);

    Console.WriteLine("Starting consumers...");
    var cts = new CancellationTokenSource();
    var consumeTasks = new Task[3];
    consumeTasks[0] = Task.Run(() => consumer1.Consume(cts.Token));
    consumeTasks[1] = Task.Run(() => consumer2.ConsumeWithExplicitCommit(1, cts.Token));
    consumeTasks[2] = Task.Run(() => consumer3.ConsumeWithExplicitCommit(1, cts.Token, true));
    LogWithTime("Finished starting consumers");

    Console.WriteLine($"Producing messages");
    Producer.Produce(topic1, numMessages: 3, config, handleDelivery: false, isTombstone: false, explicitPartitions: true);
    await Producer.ProduceAsync(topic2, numMessages: 3, config, isTombstone: false, explicitPartitions: true);
    LogWithTime("Finished producing messages");

    // Wait for all messages to be consumed
    // This assumes that the topics all start empty, and ultimately 6 messages end up consumed from topic 3
    Console.WriteLine($"Waiting for final consumption...");
    var deadline = DateTime.UtcNow.AddSeconds(180);
    while (true)
    {
        var consumed = Volatile.Read(ref topic3ConsumeCount);
        if (consumed >= 6)
        {
            Console.WriteLine($"All messages produced and consumed");
            break;
        }

        if (DateTime.UtcNow > deadline)
        {
            Console.WriteLine($"Exiting consumer: did not consume all messages: {consumed}");
            break;
        }

        await Task.Delay(1000);
    }

    LogWithTime("Finished waiting for messages");

    Console.WriteLine($"Waiting for graceful exit...");
    cts.Cancel();

    await Task.WhenAny(
        Task.WhenAll(consumeTasks),
        Task.Delay(TimeSpan.FromSeconds(5)));

    LogWithTime("Shut down complete");


    void HandleTopic3(ConsumeResult<string, string> consumeResult)
    {
        using var scope = CreateScope(consumeResult, "kafka.consume");
        Handle(consumeResult);

        var consumeCount = Interlocked.Increment(ref topic3ConsumeCount);
        Console.WriteLine($"Consumed message {consumeCount} in Topic({topic3})");
    }

    void HandleAndProduceToTopic2(ConsumeResult<string, string> consumeResult)
        => HandleAndProduce(consumeResult, topic2);

    void HandleAndProduceToTopic3(ConsumeResult<string, string> consumeResult)
        => HandleAndProduce(consumeResult, topic3);

    void HandleAndProduce(ConsumeResult<string, string> consumeResult, string produceToTopic)
    {
        using var outer = SampleHelpers.CreateScope("manual.outer");
        using var scope = CreateScope(consumeResult, "kafka.consume");
        using var inner = SampleHelpers.CreateScope("manual.inner");
        Handle(consumeResult);

        Console.WriteLine($"Producing to {produceToTopic}");
        Producer.Produce(produceToTopic, numMessages: 1, config, handleDelivery: true, isTombstone: false, explicitPartitions: true);
    }
}

async Task RunBatchProcessingScenario()
{
    // Create Topics
    var topicPrefix = "data-streams-batch-processing";

    var topic1 = $"{topicPrefix}-1";
    var topic2 = $"{topicPrefix}-2";
    var allTopics = new[] { topic1, topic2};
    var topic2ConsumeCount = 0;

    List<ConsumeResult<string, string>> _fanInMessages = new();

    var config = Config.Create();
    Console.WriteLine("Creating topics...");
    foreach (var topic in allTopics)
    {
        await TopicHelpers.TryDeleteTopic(topic, config);
        await TopicHelpers.TryCreateTopic(topic, numPartitions: 3, replicationFactor: 1, config);
    }

    LogWithTime("Finished creating topics");

    Console.WriteLine($"Creating consumers...");
    var fanInConsumer = Consumer.Create(topic1, "fan-in-consumer", HandleFanIn);
    var finalConsumer = Consumer.Create(topic2, "topic-2-consumer", HandleTopic2);

    Console.WriteLine("Starting consumers...");
    var cts = new CancellationTokenSource();
    var consumeTasks = new Task[2];
    consumeTasks[0] = Task.Run(() => finalConsumer.Consume(cts.Token));
    consumeTasks[1] = Task.Run(() => fanInConsumer.Consume(cts.Token));
    LogWithTime("Finished starting consumers");

    Console.WriteLine($"Producing messages");
    Producer.Produce(topic1, numMessages: 3, config, handleDelivery: true, isTombstone: false, explicitPartitions: true);
    LogWithTime("Finished producing messages");

    // Wait for all messages to be consumed
    // This assumes that the topics all start empty, and ultimately 2 messages end up consumed from topic 2
    Console.WriteLine($"Waiting for final consumption...");
    var deadline = DateTime.UtcNow.AddSeconds(30);
    while (true)
    {
        var consumed = Volatile.Read(ref topic2ConsumeCount);
        if (consumed >= 3)
        {
            Console.WriteLine($"All messages produced and consumed");
            break;
        }

        if (DateTime.UtcNow > deadline)
        {
            Console.WriteLine($"Exiting consumer: did not consume all messages: {consumed}");
            break;
        }

        await Task.Delay(1000);
    }

    // give some time to autocommit offsets
    await Task.Delay(100);
    LogWithTime("Finished waiting for messages");

    Console.WriteLine($"Waiting for graceful exit...");
    cts.Cancel();

    await Task.WhenAny(
        Task.WhenAll(consumeTasks),
        Task.Delay(TimeSpan.FromSeconds(5)));

    LogWithTime("Shut down complete");


    void HandleTopic2(ConsumeResult<string, string> consumeResult)
    {
        using var s = SampleHelpers.CreateScopeWithPropagation(
            "kafka.consume",
            consumeResult.Message.Headers,
            ConsumerBase.ExtractValues);
        Handle(consumeResult);

        var consumeCount = Interlocked.Increment(ref topic2ConsumeCount);
        Console.WriteLine($"Consumed message {consumeCount} in Topic({topic2})");
    }

    void HandleFanIn(ConsumeResult<string, string> consumeResult)
    {
        Handle(consumeResult);

        _fanInMessages.Add(consumeResult);
        if (_fanInMessages.Count == 3)
        {
            var iteration = 1;
            foreach (var fanInMessage in _fanInMessages)
            {
                using (SampleHelpers.CreateScopeWithPropagation(
                           "kafka.consume",
                           fanInMessage.Message.Headers,
                           ConsumerBase.ExtractValues))
                {
                    Console.WriteLine($"Producing to {topic2} - {iteration++} of 3");
                    Producer.Produce(topic2, numMessages: 1, config, handleDelivery: true, isTombstone: false, explicitPartitions: true);
                }
            }
        }

        Console.WriteLine($"Consumed message {_fanInMessages.Count} in Topic({topic1})");
    }
}

void Handle(ConsumeResult<string, string> consumeResult)
{
    var kafkaMessage = consumeResult.Message;
    Console.WriteLine($"Consuming Key({kafkaMessage.Key}), Topic({consumeResult.TopicPartitionOffset})");

    var sampleMessage = JsonConvert.DeserializeObject<SampleMessage>(kafkaMessage.Value);
    Console.WriteLine($"Received {(sampleMessage.IsProducedAsync ? "async" : "sync")} message for Key({kafkaMessage.Key})");
}

void LogWithTime(string message)
{
    Console.WriteLine($"{message}: {sw.Elapsed:g}");
    sw.Restart();
}

IDisposable CreateScope(ConsumeResult<string, string> consumeResult, string operationName)
{
    if (!extractScopesManually)
    {
        return new NoOpDisposable();
    }

    return SampleHelpers.CreateScopeWithPropagation(
        operationName,
        consumeResult.Message.Headers,
        ConsumerBase.ExtractValues);
}

class NoOpDisposable : IDisposable
{
    public void Dispose() { }
}
