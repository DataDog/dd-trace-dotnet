// <copyright file="ServiceStackRedisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class ServiceStackRedisFixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    public int Port => Container.GetMappedPublicPort(6379);

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("SERVICESTACK_REDIS_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(6379)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
            .WithImage("redis:4-alpine")
            .WithName("servicestackredis")
            .WithPortBinding(6379, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"))
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
