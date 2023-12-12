// <copyright file="StackExchangeRedisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class StackExchangeRedisFixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    public string ReplicaHostname => Replica.Hostname;

    public int Port => Container.GetMappedPublicPort(6379);

    public int ReplicaPort => Replica.GetMappedPublicPort(6379);

    protected IContainer Container => GetResource<IContainer>("container");

    protected IContainer Replica => GetResource<IContainer>("replica");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("STACKEXCHANGE_REDIS_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(6379)},{Replica.Hostname}:{Replica.GetMappedPublicPort(6379)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var network = new NetworkBuilder()
            .WithName("stackexchangeredis")
            .Build();

        var container = new ContainerBuilder()
            .WithImage("redis:4-alpine")
            .WithName("stackexchangeredis")
            .WithHostname("stackexchangeredis")
            .WithPortBinding(6379, true)
            .WithNetwork(network)
            .WithCommand("redis-server", "--bind", "0.0.0.0")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        var replica = new ContainerBuilder()
            .WithImage("redis:4-alpine")
            .WithName("stackexchangeredis-replica")
            .WithHostname("stackexchangeredis-replica")
            .WithPortBinding(6379, true)
            .WithNetwork(network)
            .WithCommand("redis-server", "--bind", "0.0.0.0", "--slaveof", "stackexchangeredis", "6379")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .DependsOn(container)
            .Build();

        await container.StartAsync();
        await replica.StartAsync();

        registerResource("container", container);
        registerResource("replica", replica);
        registerResource("network", network);
    }
}
