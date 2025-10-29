// <copyright file="RabbitMqFixture.cs" company="Datadog">
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
/// Provides a RabbitMQ container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class RabbitMqFixture : ContainerFixture
{
    private const int RabbitMqPort = 5672;
    private const int ManagementPort = 15672;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("RABBITMQ_HOST", Container.Hostname);
        yield return new("RABBITMQ_PORT", Container.GetMappedPublicPort(RabbitMqPort).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("rabbitmq:3-management")
                       .WithPortBinding(RabbitMqPort, true)
                       .WithPortBinding(ManagementPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilHttpRequestIsSucceeded(r => r
                               .ForPort(ManagementPort)
                               .ForPath("/api/health/checks/alarms")
                               .ForStatusCode(System.Net.HttpStatusCode.OK)))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
