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
    private const string PostgresPassword = "postgres";
    private const string PostgresUser = "postgres";
    private const string PostgresDatabase = "postgres";

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        var host = Container.Hostname;
        var port = Container.GetMappedPublicPort(PostgresPort);
        var connectionString = $"Host={host};Port={port};Username={PostgresUser};Password={PostgresPassword};Database={PostgresDatabase}";

        yield return new("POSTGRES_CONNECTION_STRING", connectionString);
        yield return new("POSTGRES_HOST", host);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
                       .WithImage("postgres:10.5-alpine")
                       .WithPortBinding(PostgresPort, true)
                       .WithEnvironment("POSTGRES_PASSWORD", PostgresPassword)
                       .WithEnvironment("POSTGRES_USER", PostgresUser)
                       .WithEnvironment("POSTGRES_DB", PostgresDatabase)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(PostgresPort))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
