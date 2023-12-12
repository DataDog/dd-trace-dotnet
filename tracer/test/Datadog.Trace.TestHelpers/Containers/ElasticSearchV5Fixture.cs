// <copyright file="ElasticSearchV5Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class ElasticSearchV5Fixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("ELASTICSEARCH5_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(9200)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
            .WithImage("docker.elastic.co/elasticsearch/elasticsearch:5.6.16")
            .WithName("elasticsearch5")
            .WithPortBinding(9200, true)
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("started"))
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
