// <copyright file="RabbitMqFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class RabbitMqFixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("RABBITMQ_HOST", Container.Hostname);
        yield return new("RABBITMQ_PORT", Container.GetMappedPublicPort(5672).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
            .WithImage("rabbitmq:3-management")
            .WithName("rabbitmq")
            .WithPortBinding(5672, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Server startup complete"))
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
