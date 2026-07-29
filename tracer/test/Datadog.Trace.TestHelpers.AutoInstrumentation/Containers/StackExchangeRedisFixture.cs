// <copyright file="StackExchangeRedisFixture.cs" company="Datadog">
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

public class StackExchangeRedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;
    private const string PrimaryAlias = "stackexchangeredis";

    private Resources? _resources;

    public string PrimaryHost => _resources!.PrimaryContainer.Hostname;

    public ushort PrimaryPort => _resources!.PrimaryContainer.GetMappedPublicPort(RedisPort);

    public string ReplicaHost => _resources!.ReplicaContainer.Hostname;

    public ushort ReplicaPort => _resources!.ReplicaContainer.GetMappedPublicPort(RedisPort);

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (_resources is null)
        {
            yield break;
        }

        yield return new("STACKEXCHANGE_REDIS_HOST", $"{PrimaryHost}:{PrimaryPort},{ReplicaHost}:{ReplicaPort}");
        yield return new("STACKEXCHANGE_REDIS_SINGLE_HOST", $"{_resources.SingleContainer.Hostname}:{_resources.SingleContainer.GetMappedPublicPort(RedisPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized with the image version in docker-compose.yml.
        const string image = "redis:4-alpine";

        var network = new NetworkBuilder().Build();
        var primaryContainer = new ContainerBuilder(image)
                              .WithCommand("redis-server", "--bind", "0.0.0.0")
                              .WithNetwork(network)
                              .WithNetworkAliases(PrimaryAlias)
                              .WithPortBinding(RedisPort, true)
                              .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                              .Build();
        var replicaContainer = new ContainerBuilder(image)
                              .WithCommand("redis-server", "--bind", "0.0.0.0", "--slaveof", PrimaryAlias, "6379")
                              .WithNetwork(network)
                              .WithPortBinding(RedisPort, true)
                              .WithWaitStrategy(
                                   Wait.ForUnixContainer()
                                       .UntilInternalTcpPortIsAvailable(RedisPort)
                                       .UntilCommandIsCompleted("sh", "-c", "redis-cli info replication | grep -q 'master_link_status:up'"))
                              .Build();
        var singleContainer = new ContainerBuilder(image)
                             .WithCommand("redis-server", "--bind", "0.0.0.0")
                             .WithNetwork(network)
                             .WithPortBinding(RedisPort, true)
                             .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                             .Build();
        var resources = new Resources(primaryContainer, replicaContainer, singleContainer, network);

        try
        {
            await network.CreateAsync().ConfigureAwait(false);
            await primaryContainer.StartAsync().ConfigureAwait(false);
            await Task.WhenAll(replicaContainer.StartAsync(), singleContainer.StartAsync()).ConfigureAwait(false);
        }
        catch
        {
            await resources.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _resources = resources;
        registerResource("resources", resources);
    }

    private sealed class Resources : IAsyncDisposable
    {
        public Resources(IContainer primaryContainer, IContainer replicaContainer, IContainer singleContainer, INetwork network)
        {
            PrimaryContainer = primaryContainer;
            ReplicaContainer = replicaContainer;
            SingleContainer = singleContainer;
            Network = network;
        }

        public IContainer PrimaryContainer { get; }

        public IContainer ReplicaContainer { get; }

        public IContainer SingleContainer { get; }

        private INetwork Network { get; }

        public async ValueTask DisposeAsync()
        {
            await SingleContainer.DisposeAsync().ConfigureAwait(false);
            await ReplicaContainer.DisposeAsync().ConfigureAwait(false);
            await PrimaryContainer.DisposeAsync().ConfigureAwait(false);
            await Network.DisposeAsync().ConfigureAwait(false);
        }
    }
}
