// <copyright file="MySql57Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class MySql57Fixture : ContainerFixture
{
    private const int MySqlPort = 3306;
    private const string Username = "mysqldb";
    private const string Password = "mysqldb";
    private const string Database = "world";
    private const string Image = "mysql/mysql-server:5.7@sha256:1178cdd375f758968cd834ac4057bae41307e64b7c69a9e145896e7b11f48064";

    private IContainer? _container;

    public string? Host => _container?.Hostname;

    public ushort? Port => _container?.GetMappedPublicPort(MySqlPort);

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (Host is { } host && Port is { } port)
        {
            yield return new("MYSQL57_HOST", host);
            yield return new("MYSQL57_PORT", port.ToString());
        }
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // The old MySql.Data tests are marked ArmUnsupported, but their collection also contains MySQL 8 tests.
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            return;
        }

        var container = new ContainerBuilder(Image)
                       .WithPortBinding(MySqlPort, true)
                       .WithEnvironment("MYSQL_DATABASE", Database)
                       .WithEnvironment("MYSQL_ROOT_PASSWORD", Password)
                       .WithEnvironment("MYSQL_USER", Username)
                       .WithEnvironment("MYSQL_PASSWORD", Password)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("mysqladmin", "ping", "--silent", "-h", "localhost", "-u", "root", $"-p{Password}"))
                       .Build();

        await container.StartAsync().ConfigureAwait(false);

        _container = container;
        registerResource("container", container);
    }
}
