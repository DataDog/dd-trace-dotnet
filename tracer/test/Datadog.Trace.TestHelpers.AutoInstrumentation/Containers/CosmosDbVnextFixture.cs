// <copyright file="CosmosDbVnextFixture.cs" company="Datadog">
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

public class CosmosDbVnextFixture : ContainerFixture
{
    private const ushort GatewayPort = 8081;
    private const ushort HealthPort = 8080;

    // Keep synchronized with the image version in docker-compose.yml.
    private const string Image = "mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-preview@sha256:54d7bc334494c50cea867c270880671a7db080626a9732832b34c0d69342f9b0";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(GatewayPort);

    public string Endpoint => $"https://{Host}:{Port}";

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("COSMOSDB_ENDPOINT", Endpoint);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       .WithCommand("--protocol", "https")
                       .WithPortBinding(GatewayPort, true)
                       .WithPortBinding(HealthPort, true)
                       .WithWaitStrategy(
                            Wait.ForUnixContainer()
                                .UntilHttpRequestIsSucceeded(
                                     request => request.ForPort(HealthPort).ForPath("/ready"),
                                     strategy => strategy.WithTimeout(TimeSpan.FromMinutes(2))))
                       .Build();

        registerResource("container", container);
        await StartContainerAsync(container).ConfigureAwait(false);
    }
}
