// <copyright file="MySqlFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers;

public class MySqlFixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MYSQL_HOST", Container.Hostname);
        yield return new("MYSQL_PORT", Container.GetMappedPublicPort(3306).ToString());
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder()
            .WithImage("mysql/mysql-server:8.0")
            .WithName("mysql")
            .WithEnvironment("MYSQL_USER", "mysqldb")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", "mysqldb")
            .WithEnvironment("MYSQL_PASSWORD", "mysqldb")
            .WithEnvironment("MYSQL_DATABASE", "world")
            .WithPortBinding(3306, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("ready for connections"))
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
