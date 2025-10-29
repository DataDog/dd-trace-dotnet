// <copyright file="ElasticsearchFixtureBase.cs" company="Datadog">
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

public abstract class ElasticsearchFixtureBase : ContainerFixture
{
    private const int ElasticsearchPort = 9200;

    protected abstract string ImageTag { get; }

    protected abstract string EnvironmentVariableName { get; }

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new(EnvironmentVariableName, $"{Container.Hostname}:{Container.GetMappedPublicPort(ElasticsearchPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image versions with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage($"elasticsearch:{ImageTag}")
                       .WithPortBinding(ElasticsearchPort, true)
                       .WithEnvironment("discovery.type", "single-node")
                       .WithEnvironment("xpack.security.enabled", "false")
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilHttpRequestIsSucceeded(r => r
                               .ForPort(ElasticsearchPort)
                               .ForPath("/_cluster/health")
                               .ForStatusCode(System.Net.HttpStatusCode.OK)))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
