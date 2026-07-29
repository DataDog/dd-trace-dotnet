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
    private const int SqlServerPort = 1433;
    private const string Password = "Strong!Passw0rd";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:latest@sha256:2cd0aec4a3bfc3cf9205bed3f7922f4c6208f7c767dc62edcee308d0fd7d56d0";
    private const string AzureSqlEdgeImage = "mcr.microsoft.com/azure-sql-edge:latest@sha256:902628a8be89e35dfb7895ca31d602974c7bafde4d583a0d0873844feb1c42cf";

    private IContainer? _container;

    public string? HostAndPort => _container is null ? null : $"{_container.Hostname},{_container.GetMappedPublicPort(SqlServerPort)}";

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (HostAndPort is { } hostAndPort)
        {
            yield return new("SQLSERVER_CONNECTION_STRING", $"Server={hostAndPort};User=sa;Password={Password};TrustServerCertificate=True");
        }
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Windows CI provides LocalDB, and callers can explicitly select another existing SQL Server.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLSERVER_CONNECTION_STRING")))
        {
            return;
        }

        // mssql/server has no native arm64 image, so use Azure SQL Edge on arm64.
        var image = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? AzureSqlEdgeImage : SqlServerImage;
        var container = new ContainerBuilder(image)
                       .WithPortBinding(SqlServerPort, true)
                       .WithEnvironment("ACCEPT_EULA", "Y")
                       .WithEnvironment("MSSQL_SA_PASSWORD", Password)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("SQL Server is now ready for client connections"))
                       .Build();

        await container.StartAsync().ConfigureAwait(false);

        _container = container;
        registerResource("container", container);
    }
}
