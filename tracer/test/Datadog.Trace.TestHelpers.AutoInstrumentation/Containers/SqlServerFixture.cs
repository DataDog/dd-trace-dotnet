// <copyright file="SqlServerFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Images;
using Testcontainers.MsSql;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class SqlServerFixture : ContainerFixture
{
    private MsSqlContainer? _container;

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (_container is null)
        {
            // SQLSERVER_CONNECTION_STRING was already set externally (e.g., by
            // the CI pipeline pointing at LocalDB) or the sample will fall back
            // to its default connection string.
            yield break;
        }

        // Build the connection string manually to match the old docker-compose format
        // (Server + User + Password only). The default GetConnectionString() includes
        // Database=master and User Id=sa which adds db.name/db.user tags and changes
        // the peer.service source from out.host to db.name.
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(MsSqlBuilder.MsSqlPort);
        var connectionString = $"Server={host},{port};User=sa;Password=Strong!Passw0rd;TrustServerCertificate=True";
        yield return new("SQLSERVER_CONNECTION_STRING", connectionString);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // If a connection string is already provided (e.g., CI pipeline pointing
        // at LocalDB or a pre-existing SQL Server), skip container creation.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")))
        {
            return;
        }

        // mssql/server has no native arm64 image; use Azure SQL Edge on arm64 instead
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? new DockerImage("mcr.microsoft.com/azure-sql-edge:latest")
            : new DockerImage("mcr.microsoft.com/mssql/server:2022-latest");

        var container = new MsSqlBuilder(image)
                       .WithPassword("Strong!Passw0rd")
                       .Build();

        await container.StartAsync();

        _container = container;
        registerResource("container", container);
    }
}
