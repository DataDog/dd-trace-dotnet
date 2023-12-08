// <copyright file="SqlServerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Testcontainers.MsSql;

namespace Datadog.Trace.TestHelpers.Containers;

public class SqlServerFixture : ContainerFixture
{
    public string Hostname => Container.Hostname;

    public int Port => Container.GetMappedPublicPort(MsSqlBuilder.MsSqlPort);

    protected MsSqlContainer Container => GetResource<MsSqlContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("SQLSERVER_CONNECTION_STRING", $"Server={Hostname},{Port};User={MsSqlBuilder.DefaultUsername};Password={MsSqlBuilder.DefaultPassword}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:latest")
            .WithName("sqlserver")
            .Build();

        await container.StartAsync();

        registerResource("container", container);
    }
}
