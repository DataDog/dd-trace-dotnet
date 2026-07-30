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

public class RabbitMqFixture : ContainerFixture
{
    private const int RabbitMqPort = 5672;
    private const string Image = "rabbitmq:3-management@sha256:e582c0bc7766f3342496d8485efb5a1df782b5ce3886ad017e2eaae442311f69";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(RabbitMqPort);

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("RABBITMQ_HOST", Host);
        yield return new("RABBITMQ_PORT", Port.ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // rabbitmq-diagnostics can race startup and create a root-owned Erlang cookie.
        var container = new ContainerBuilder(Image)
                       .WithPortBinding(RabbitMqPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RabbitMqPort))
                       .Build();

        registerResource("container", container);
        await container.StartAsync().ConfigureAwait(false);
    }
}
