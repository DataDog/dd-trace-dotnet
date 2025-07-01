// <copyright file="KafkaBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Datadog.Trace;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [JsonExporterAttribute.Brief]
    [JsonExporterAttribute.Full]
    [JsonExporterAttribute.BriefCompressed]
    [JsonExporterAttribute.FullCompressed]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class KafkaBenchmark
    {
        private IProducer<long, string> _producer = null!;
        private IConsumer<long, string> _consumer = null!;
        private const string BootstrapServers = "localhost:9092";
        private string Topic = "test-topic";
        private int MessageCount = 1;
        private const int MessageSize = 32;
        private const string ConsumerGroupId = "benchmark-consumer-group";

        [GlobalSetup]
        public void Setup()
        {
            Console.WriteLine("Setting up Kafka benchmark...");
            var settings = TracerSettings.FromDefaultSources();
            Console.WriteLine($"Tracing enabled: {settings.TraceEnabled}");
            Console.WriteLine($"Agent host: {settings.Exporter.AgentUri}");
            Console.WriteLine($"Environment: {settings.Environment}");

            MessageCount = int.Parse(Environment.GetEnvironmentVariable("NUM_MESSAGES") ?? "1000");
            Topic = Environment.GetEnvironmentVariable("KAFKA_TOPIC") ?? "test-topic";
            Console.WriteLine($"Sending and receiving {MessageCount} messages to/from {Topic}...");

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = BootstrapServers,
                EnableDeliveryReports = true,
                Acks = Acks.All,
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 100,
            };

            _producer = new ProducerBuilder<long, string>(producerConfig).
                SetKeySerializer(Serializers.Int64).
                SetValueSerializer(Serializers.Utf8).
                SetErrorHandler((_, e) => Console.WriteLine($"Producer Error: {e.Reason}.")).
                Build();

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = BootstrapServers,
                GroupId = ConsumerGroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnableAutoOffsetStore = false
            };

            _consumer = new ConsumerBuilder<long, string>(consumerConfig).
                SetKeyDeserializer(Deserializers.Int64).
                SetValueDeserializer(Deserializers.Utf8).
                SetErrorHandler((_, e) => Console.WriteLine($"Consumer Error: {e.Reason}.")).
                Build();

            CreateTopics(BootstrapServers, new List<string> { Topic });
            
            // Subscribe to the topic
            _consumer.Subscribe(Topic);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _producer?.Flush();
            _producer?.Dispose();
            _consumer?.Close();
            _consumer?.Dispose();
        }

        [IterationCleanup]
        public void IterationCleanup()
        {
            _producer?.Flush();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            _producer?.Flush();
        }

        [Benchmark]
        public void ProduceAndConsumeMessages()
        {
            var largeContent = new string('x', MessageSize);
            
            for (int i = 0; i < MessageCount; i++)
            {
                var msg = new Msg
                {
                    Id = i.ToString(),
                    Name = i.ToString(),
                    Description = largeContent
                };

                var headers = new Headers();
                for (var j = 0; j < 5; j++)
                {
                    headers.Add("key" + j, Encoding.UTF8.GetBytes("value" + j));
                }

                var message = new Message<long, string>
                {
                    Key = DateTime.UtcNow.Ticks,
                    Value = JsonSerializer.Serialize(msg),
                    Headers = headers
                };

                _producer.Produce(Topic, message);
                _producer.Flush();

                // Consume the message synchronously
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(10));
                if (consumeResult != null)
                {
                    // Verify we got the expected message
                    var receivedMsg = JsonSerializer.Deserialize<Msg>(consumeResult.Message.Value);
                    if (receivedMsg?.Id != i.ToString())
                    {
                        throw new Exception($"Message mismatch: expected {i}, got {receivedMsg?.Id}");
                    }
                    
                    // Commit the offset
                    _consumer.Commit(consumeResult);
                }
                else
                {
                    throw new Exception($"Failed to consume message {i} within timeout");
                }
            }
        }

        /// <summary>
        /// Creates topics if they don't exist
        /// </summary>
        private static void CreateTopics(string bootstrapServers, List<string> topics)
        {
            var config = new ClientConfig
            {
                BootstrapServers = bootstrapServers
            };

            using var adminClient = new AdminClientBuilder(config).Build();
            
            foreach (var topic in topics)
            {
                try
                {
                    Console.WriteLine($"Creating topic {topic}...");
                    adminClient.CreateTopicsAsync(new List<TopicSpecification> {
                        new()
                        {
                            Name = topic,
                            NumPartitions = 1,
                            ReplicationFactor = 1
                        }
                    }).Wait();
                    Console.WriteLine($"Topic {topic} created successfully");
                }
                catch (AggregateException ex) when (ex.InnerException is CreateTopicsException createEx)
                {
                    if (createEx.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                    {
                        Console.WriteLine($"Topic {topic} already exists, skipping creation");
                    }
                    else
                    {
                        Console.WriteLine($"Error creating topic {topic}: {createEx.Results[0].Error.Reason}");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unexpected error creating topic {topic}: {ex.Message}");
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Message model for benchmarking
    /// </summary>
    public class Msg
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
