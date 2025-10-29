// <copyright file="KafkaFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

/// <summary>
/// Provides Kafka and Zookeeper containers for integration tests.
/// Keep synchronized image versions with docker-compose.yml
/// Note: Kafka requires Zookeeper, so both containers are managed together.
/// </summary>
public class KafkaFixture : ContainerFixture
{
    private const int ZookeeperPort = 2181;
    private const int KafkaInternalPort = 29092;
    private const int KafkaExternalPort = 9092;

    protected INetwork Network => GetResource<INetwork>("network");

    protected IContainer ZookeeperContainer => GetResource<IContainer>("zookeeper");

    protected IContainer KafkaContainer => GetResource<IContainer>("kafka");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("KAFKA_BROKER_HOST", $"{KafkaContainer.Hostname}:{KafkaContainer.GetMappedPublicPort(KafkaExternalPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Create a network for Kafka and Zookeeper to communicate
        var network = new NetworkBuilder()
                     .WithName($"kafka-network-{Guid.NewGuid():N}")
                     .Build();

        await network.CreateAsync();
        registerResource("network", network);

        // Start Zookeeper first
        var zookeeperContainer = new ContainerBuilder()
                                .WithImage("confluentinc/cp-zookeeper:6.1.1")
                                .WithNetwork(network)
                                .WithNetworkAliases("zookeeper")
                                .WithPortBinding(ZookeeperPort, true)
                                .WithEnvironment("ZOOKEEPER_CLIENT_PORT", ZookeeperPort.ToString())
                                .WithEnvironment("ZOOKEEPER_TICK_TIME", "2000")
                                .WithWaitStrategy(Wait.ForUnixContainer()
                                    .UntilPortIsAvailable(ZookeeperPort))
                                .Build();

        await zookeeperContainer.StartAsync();
        registerResource("zookeeper", zookeeperContainer);

        // Start Kafka broker
        var kafkaContainer = new ContainerBuilder()
                            .WithImage("confluentinc/cp-kafka:6.1.1")
                            .WithNetwork(network)
                            .WithNetworkAliases("kafka-broker")
                            .WithPortBinding(KafkaExternalPort, true)
                            .WithEnvironment("KAFKA_BROKER_ID", "1")
                            .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", $"zookeeper:{ZookeeperPort}")
                            .WithEnvironment("KAFKA_LISTENER_SECURITY_PROTOCOL_MAP", "PLAINTEXT:PLAINTEXT,PLAINTEXT_HOST:PLAINTEXT")
                            .WithEnvironment("KAFKA_ADVERTISED_LISTENERS", $"PLAINTEXT://kafka-broker:{KafkaInternalPort},PLAINTEXT_HOST://localhost:{KafkaExternalPort}")
                            .WithEnvironment("KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR", "1")
                            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_MIN_ISR", "1")
                            .WithEnvironment("KAFKA_TRANSACTION_STATE_LOG_REPLICATION_FACTOR", "1")
                            .WithWaitStrategy(Wait.ForUnixContainer()
                                .UntilPortIsAvailable(KafkaInternalPort))
                            .Build();

        await kafkaContainer.StartAsync();
        registerResource("kafka", kafkaContainer);
    }
}
