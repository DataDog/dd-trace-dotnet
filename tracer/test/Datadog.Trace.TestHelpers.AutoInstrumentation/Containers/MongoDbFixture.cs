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

public class MongoDbFixture : ContainerFixture
{
    private const int MongoDbPort = 27017;
    private const string Image = "mongo:5.0.31@sha256:54bcd8da3ea5eec561b68c605046c55c6b304387dc4c2bf5b3a5f5064fbb7495";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(MongoDbPort);

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MONGO_HOST", Host);
        yield return new("MONGO_PORT", Port.ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       .WithPortBinding(MongoDbPort, true)
                       .WithWaitStrategy(
                            Wait.ForUnixContainer()
                                .UntilCommandIsCompleted(
                                     ["mongo", "--quiet", "--eval", "db.adminCommand({ ping: 1 }).ok"],
                                     strategy => strategy.WithTimeout(TimeSpan.FromMinutes(2))))
                       .Build();

        registerResource("container", container);
        await StartContainerAsync(container).ConfigureAwait(false);
    }
}
