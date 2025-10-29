// <copyright file="MongoDbFixture.cs" company="Datadog">
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
/// Provides a MongoDB container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class MongoDbFixture : ContainerFixture
{
    private const int MongoPort = 27017;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MONGO_HOST", $"{Container.Hostname}:{Container.GetMappedPublicPort(MongoPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("mongo:4.0.9")
                       .WithPortBinding(MongoPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("/bin/bash", "-c", "mongosh --eval \"db.version()\" || mongo --eval \"db.version()\""))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
