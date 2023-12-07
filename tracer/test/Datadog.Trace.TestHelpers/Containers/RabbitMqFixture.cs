// <copyright file="RabbitMqFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Testcontainers.RabbitMq;

namespace Datadog.Trace.TestHelpers.Containers;

public class RabbitMqFixture : ContainerFixture
{
    protected RabbitMqContainer Container => GetResource<RabbitMqContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("RABBITMQ_HOST", Container.Hostname);
        yield return new("RABBITMQ_PORT", Container.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new RabbitMqBuilder()
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
