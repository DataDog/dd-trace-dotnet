// <copyright file="SqlServerFixture.cs" company="Datadog">
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
/// Provides a SQL Server container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class SqlServerFixture : ContainerFixture
{
    private const int SqlServerPort = 1433;
    private const string SaPassword = "Strong!Passw0rd";

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        var connectionString = $"Server={Container.Hostname},{Container.GetMappedPublicPort(SqlServerPort)};User=sa;Password={SaPassword};TrustServerCertificate=true";
        yield return new("SQLSERVER_CONNECTION_STRING", connectionString);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                       .WithPortBinding(SqlServerPort, true)
                       .WithEnvironment("ACCEPT_EULA", "Y")
                       .WithEnvironment("SA_PASSWORD", SaPassword)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("/opt/mssql-tools/bin/sqlcmd", "-S", "localhost", "-U", "sa", "-P", SaPassword, "-Q", "SELECT 1"))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
