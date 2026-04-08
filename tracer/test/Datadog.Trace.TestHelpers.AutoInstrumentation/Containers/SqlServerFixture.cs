// <copyright file="SqlServerFixture.cs" company="Datadog">
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

public class SqlServerFixture : ContainerFixture
{
    private const int MsSqlPort = 1433;
    private const string Password = "Strong!Passw0rd";

    // Keep synchronized with docker-compose.yml pre-pull entries
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-latest";
    private const string AzureSqlEdgeImage = "mcr.microsoft.com/azure-sql-edge:latest";

    private IContainer? _container;

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (_container is null)
        {
            // SQLSERVER_CONNECTION_STRING was already set externally (e.g., by
            // the CI pipeline pointing at LocalDB) or the sample will fall back
            // to its default connection string.
            yield break;
        }

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(MsSqlPort);
        yield return new("SQLSERVER_CONNECTION_STRING", $"Server={host},{port};User=sa;Password={Password};TrustServerCertificate=True");
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
            ? AzureSqlEdgeImage
            : SqlServerImage;

        var container = new ContainerBuilder(image)
                       .WithPortBinding(MsSqlPort, true)
                       .WithEnvironment("ACCEPT_EULA", "Y")
                       .WithEnvironment("MSSQL_SA_PASSWORD", Password)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
                       .Build();

        await container.StartAsync();

        _container = container;
        registerResource("container", container);
    }
}
