// <copyright file="ServiceStackRedisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class ServiceStackRedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("SERVICESTACK_REDIS_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(RedisPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder("redis:4-alpine")
                       .WithCommand("redis-server", "--bind", "0.0.0.0")
                       .WithPortBinding(RedisPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
