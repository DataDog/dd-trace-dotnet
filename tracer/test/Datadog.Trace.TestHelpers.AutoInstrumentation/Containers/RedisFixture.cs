// <copyright file="RedisFixture.cs" company="Datadog">
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

/// <summary>
/// Provides a Redis container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class RedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        var host = $"{Container.Hostname}:{Container.GetMappedPublicPort(RedisPort)}";
        yield return new("SERVICESTACK_REDIS_HOST", host);
        yield return new("STACKEXCHANGE_REDIS_HOST", host);
        yield return new("REDIS_HOST", host);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("redis:4-alpine")
                       .WithPortBinding(RedisPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("redis-cli", "ping"))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
