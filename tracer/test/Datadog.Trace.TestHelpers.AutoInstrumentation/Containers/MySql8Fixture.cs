// <copyright file="MySql8Fixture.cs" company="Datadog">
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

public class MySql8Fixture : ContainerFixture
{
    private const int MySqlPort = 3306;
    private const string Username = "mysqldb";
    private const string Password = "mysqldb";
    private const string Database = "world";
    private const string Image = "mysql/mysql-server:8.0@sha256:d6c8301b7834c5b9c2b733b10b7e630f441af7bc917c74dba379f24eeeb6a313";

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(MySqlPort);

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MYSQL_HOST", Host);
        yield return new("MYSQL_PORT", Port.ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       .WithPortBinding(MySqlPort, true)
                       .WithEnvironment("MYSQL_DATABASE", Database)
                       .WithEnvironment("MYSQL_ROOT_PASSWORD", Password)
                       .WithEnvironment("MYSQL_USER", Username)
                       .WithEnvironment("MYSQL_PASSWORD", Password)
                       .WithWaitStrategy(
                            Wait.ForUnixContainer()
                                // mysqladmin can reach the temporary server used by the image's initialization script.
                                // Wait for that phase to finish before probing the final server.
                                .UntilMessageIsLogged("MySQL init process done. Ready for start up.")
                                .UntilCommandIsCompleted("mysqladmin", "ping", "--silent", "-h", "localhost", "-u", "root", $"-p{Password}"))
                       .Build();

        await container.StartAsync().ConfigureAwait(false);

        registerResource("container", container);
    }
}
