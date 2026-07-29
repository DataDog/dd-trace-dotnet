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
using Testcontainers.Kafka;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class KafkaFixture : ContainerFixture
{
    private const int ZooKeeperPort = 2181;
    private const string ZooKeeperAlias = "kafka-zookeeper";
    private const string ZooKeeperImage = "confluentinc/cp-zookeeper:6.1.1@sha256:a7c0a20dce46a705300cd464e511e9c70ac55ec7e62c024867470a19ce210563";
    private const string KafkaImage = "confluentinc/cp-server:6.1.1@sha256:4a1ff92bd03e361759ba339c97b4c4b7dbb52d7cea478dda22d034261a8991e4";

    private KafkaContainer KafkaContainer => GetResource<Resources>("resources").KafkaContainer;

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("KAFKA_BROKER_HOST", KafkaContainer.GetBootstrapAddress());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var network = new NetworkBuilder().Build();
        var zooKeeperContainer = new ContainerBuilder(ZooKeeperImage)
                                .WithNetwork(network)
                                .WithNetworkAliases(ZooKeeperAlias)
                                .WithEnvironment("ZOOKEEPER_CLIENT_PORT", ZooKeeperPort.ToString())
                                .WithEnvironment("ZOOKEEPER_TICK_TIME", "2000")
                                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ZooKeeperPort))
                                .Build();
        var kafkaContainer = new KafkaBuilder(KafkaImage)
                            .WithNetwork(network)
                            .WithZooKeeper($"{ZooKeeperAlias}:{ZooKeeperPort}")
                            .WithPortBinding(KafkaBuilder.ZooKeeperPort, true)
                            .WithEnvironment("KAFKA_AUTO_CREATE_TOPICS_ENABLE", "false")
                            .Build();
        var resources = new Resources(kafkaContainer, zooKeeperContainer, network);

        try
        {
            await network.CreateAsync().ConfigureAwait(false);
            await zooKeeperContainer.StartAsync().ConfigureAwait(false);
            await kafkaContainer.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            await resources.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        registerResource("resources", resources);
    }

    private sealed class Resources : IAsyncDisposable
    {
        public Resources(KafkaContainer kafkaContainer, IContainer zooKeeperContainer, INetwork network)
        {
            KafkaContainer = kafkaContainer;
            ZooKeeperContainer = zooKeeperContainer;
            Network = network;
        }

        public KafkaContainer KafkaContainer { get; }

        private IContainer ZooKeeperContainer { get; }

        private INetwork Network { get; }

        public async ValueTask DisposeAsync()
        {
            await KafkaContainer.DisposeAsync().ConfigureAwait(false);
            await ZooKeeperContainer.DisposeAsync().ConfigureAwait(false);
            await Network.DisposeAsync().ConfigureAwait(false);
        }
    }
}
