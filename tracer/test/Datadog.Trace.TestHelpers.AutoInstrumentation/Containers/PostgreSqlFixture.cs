// <copyright file="PostgreSqlFixture.cs" company="Datadog">
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
/// Provides a PostgreSQL container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class PostgreSqlFixture : ContainerFixture
{
    private const int PostgresPort = 5432;
    private const string PostgresUser = "postgres";
    private const string PostgresPassword = "postgres";
    private const string PostgresDb = "postgres";

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("POSTGRES_HOST", Container.Hostname);
        yield return new("POSTGRES_PORT", Container.GetMappedPublicPort(PostgresPort).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("postgres:10.5")
                       .WithPortBinding(PostgresPort, true)
                       .WithEnvironment("POSTGRES_PASSWORD", PostgresPassword)
                       .WithEnvironment("POSTGRES_USER", PostgresUser)
                       .WithEnvironment("POSTGRES_DB", PostgresDb)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("pg_isready", "-U", PostgresUser))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
