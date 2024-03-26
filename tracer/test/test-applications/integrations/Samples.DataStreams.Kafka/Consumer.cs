using System;
using Confluent.Kafka;
using Samples.Kafka;

namespace Samples.DataStreams.Kafka;

internal class Consumer : ConsumerBase
{
    private readonly Action<ConsumeResult<string, string>> _handler;
    private Consumer(ConsumerConfig config, string topic, string consumerName, Action<ConsumeResult<string, string>> handler)
        : base(config, topic, consumerName)
    {
        _handler = handler;
    }

    protected override void HandleMessage(ConsumeResult<string, string> consumeResult) => _handler(consumeResult);

    public static Consumer Create(string topic, string consumerName, Action<ConsumeResult<string, string>> handler, bool enableAutoCommit = true)
    {
        Console.WriteLine($"Creating consumer '{consumerName}' and subscribing to topic {topic}");

        var config = new ConsumerConfig
        {
            BootstrapServers = Samples.Kafka.Config.KafkaBrokerHost,
            GroupId = "Samples.DataStreams.Kafka." + consumerName,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = enableAutoCommit,
            AutoCommitIntervalMs = 5, 
        };
        return new Consumer(config, topic, consumerName, handler);
    }
}
