// <copyright file="PostgresFixture.cs" company="Datadog">
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

public class PostgresFixture : ContainerFixture
{
    private const int PostgresPort = 5432;
    private const string Username = "postgres";
    private const string Password = "postgres";
    private const string Database = "postgres";
    private const string Image = "postgres:10.5-alpine@sha256:295a08ddd9efa1612c46033f0b96c3976f80f49c7ce29e05916b0af557806117";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(PostgresPort);

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("POSTGRES_CONNECTION_STRING", $"Host={Host};Port={Port};Username={Username};Password={Password};Database={Database}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       .WithPortBinding(PostgresPort, true)
                       .WithEnvironment("POSTGRES_USER", Username)
                       .WithEnvironment("POSTGRES_PASSWORD", Password)
                       .WithEnvironment("POSTGRES_DB", Database)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-U", Username))
                       .Build();

        await container.StartAsync().ConfigureAwait(false);

        registerResource("container", container);
    }
}
