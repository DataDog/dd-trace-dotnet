// <copyright file="MySqlFixture.cs" company="Datadog">
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
/// Provides a MySQL container for integration tests.
/// Keep synchronized image version with docker-compose.yml
/// </summary>
public class MySqlFixture : ContainerFixture
{
    private const int MySqlPort = 3306;
    private const string MySqlRootPassword = "mysqldb";

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MYSQL_HOST", Container.Hostname);
        yield return new("MYSQL_PORT", Container.GetMappedPublicPort(MySqlPort).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized image version with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("mysql:8.0")
                       .WithPortBinding(MySqlPort, true)
                       .WithEnvironment("MYSQL_ROOT_PASSWORD", MySqlRootPassword)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("mysqladmin", "ping", "-h", "localhost", "-u", "root", $"-p{MySqlRootPassword}"))
                       .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
