using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Newtonsoft.Json;
using Samples.DataStreams.Kafka;
using Samples.Kafka;
using Config = Samples.Kafka.Config;

// Create Topics
var sw = new Stopwatch();
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
var consumer2 = Consumer.Create(topic2, "consumer-2", HandleAndProduceToTopic3);
var consumer3 = Consumer.Create(topic3, "consumer-3", HandleTopic3);

Console.WriteLine("Starting consumers...");
var cts = new CancellationTokenSource();
var consumeTasks = new Task[3];
consumeTasks[0] = Task.Run(() => consumer1.Consume(cts.Token));
consumeTasks[1] = Task.Run(() => consumer2.Consume(cts.Token));
consumeTasks[2] = Task.Run(() => consumer3.Consume(cts.Token));


LogWithTime("Finished starting consumers");
Console.WriteLine($"Producing messages");
Producer.Produce(topic1, numMessages: 3, config, handleDelivery: true, isTombstone: false);
Producer.Produce(topic2, numMessages: 3, config, handleDelivery: true, isTombstone: false);

LogWithTime("Finished producing messages");
Console.WriteLine($"Waiting for final consumption...");

// Wait for all messages to be consumed
// This assumes that the topics all start empty, and ultimately 6 messages end up consumed from topic 3
var deadline = DateTime.UtcNow.AddSeconds(30);
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

await Task.WhenAny(Task.WhenAll(consumeTasks),
                   Task.Delay(TimeSpan.FromSeconds(5)));

LogWithTime("Shut down complete");

void HandleTopic3(ConsumeResult<string,string> consumeResult)
{
    Handle(consumeResult);

    var consumeCount = Interlocked.Increment(ref topic3ConsumeCount);
    Console.WriteLine($"Consumed message {consumeCount} in Topic({topic3})");
}

void HandleAndProduceToTopic2(ConsumeResult<string, string> consumeResult)
    => HandleAndProduce(consumeResult, topic2);

void HandleAndProduceToTopic3(ConsumeResult<string, string> consumeResult)
    => HandleAndProduce(consumeResult, topic3);

void HandleAndProduce(ConsumeResult<string,string> consumeResult, string produceToTopic)
{
    Handle(consumeResult);
    
    Console.WriteLine($"Producing to {produceToTopic}");
    Producer.Produce(produceToTopic, numMessages: 1, config, handleDelivery: true, isTombstone: false);
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
