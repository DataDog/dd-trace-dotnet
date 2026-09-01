// <copyright file="DbCommandCacheTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Data;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;
using AdoNetDbType = Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.DbType;

namespace Datadog.Trace.Tests.Util;

public class DbCommandCacheTests
{
    [Theory]
    // SQL Server keeps the host, the instance name, and the port in a single keyword, which the
    // Datadog "out.host" reports verbatim and the OpenTelemetry attributes split apart
    [InlineData(
        AdoNetDbType.SqlServer,
        "Server=tcp:myserver.database.windows.net,1434;Initial Catalog=mydb;User ID=me;Password=p",
        "myserver.database.windows.net",
        1434,
        "mydb")]
    [InlineData(
        AdoNetDbType.SqlServer,
        "Server=localhost\\SQLEXPRESS;Database=mydb;Integrated Security=true",
        "localhost",
        null,
        "SQLEXPRESS|mydb")]
    [InlineData(
        AdoNetDbType.SqlServer,
        "Data Source=localhost,1433;Initial Catalog=mydb",
        "localhost",
        null,
        "mydb")]

    // PostgreSQL and MySQL keep the port in its own keyword
    [InlineData(
        AdoNetDbType.PostgreSql,
        "Host=postgres_db;Port=5433;Database=postgres;Username=postgres;Password=p",
        "postgres_db",
        5433,
        "postgres")]
    [InlineData(
        AdoNetDbType.PostgreSql,
        "Host=postgres_db;Port=5432;Database=postgres",
        "postgres_db",
        null,
        "postgres")]
    [InlineData(
        AdoNetDbType.MySql,
        "Server=mysql_db;Port=3307;Database=world;User ID=mysqldb",
        "mysql_db",
        3307,
        "world")]

    // SQLite is embedded, so there is no server and the data source is the namespace
    [InlineData(
        AdoNetDbType.Sqlite,
        "Data Source=:memory:",
        null,
        null,
        ":memory:")]
    [InlineData(
        AdoNetDbType.Sqlite,
        "Data Source=/tmp/Sqlite-Test.db;Mode=ReadWriteCreate",
        null,
        null,
        "/tmp/Sqlite-Test.db")]

    // Oracle keeps the host, the port, and the service name in the data source
    [InlineData(
        AdoNetDbType.Oracle,
        "User Id=system;Password=p;Data Source=//oracle_db:1522/XE",
        "oracle_db",
        1522,
        "XE")]
    public void GetTagsFromDbCommand_CalculatesTheOpenTelemetryConnectionAttributes(
        string dbType,
        string connectionString,
        string expectedServerAddress,
        int? expectedServerPort,
        string expectedDbNamespace)
    {
        var tags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString), dbType);

        tags.ServerAddress.Should().Be(expectedServerAddress);
        tags.ServerPort.Should().Be(expectedServerPort);
        tags.DbNamespace.Should().Be(expectedDbNamespace);
        tags.DbType.Should().Be(dbType);
    }

    [Fact]
    public void GetTagsFromDbCommand_LeavesTheDatadogTagsUnchanged()
    {
        // The Datadog tags must keep reporting the connection string verbatim, whatever the
        // OpenTelemetry attributes are shaped into.
        const string connectionString = "Server=tcp:myserver,1434;Initial Catalog=mydb;User ID=me;Password=p";

        var tags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString), AdoNetDbType.SqlServer);

        tags.OutHost.Should().Be("tcp:myserver,1434");
        tags.DbName.Should().Be("mydb");
        tags.DbUser.Should().Be("me");
    }

    [Fact]
    public void GetTagsFromDbCommand_RecalculatesWhenTheSameConnectionStringIsUsedByAnotherProvider()
    {
        // The cache is keyed by connection string alone, but a "Server" keyword means different
        // things to different providers.
        const string connectionString = "Server=myserver\\instance1;Database=mydb";

        var sqlServerTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString), AdoNetDbType.SqlServer);
        var mySqlTags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand(connectionString), AdoNetDbType.MySql);

        sqlServerTags.DbNamespace.Should().Be("instance1|mydb");
        mySqlTags.DbNamespace.Should().Be("mydb");
        mySqlTags.ServerAddress.Should().Be("myserver\\instance1");
    }

    [Fact]
    public void GetTagsFromDbCommand_ReportsNoTagsForAnInvalidConnectionString()
    {
        var tags = DbCommandCache.GetTagsFromDbCommand(CreateDbCommand("\""), AdoNetDbType.SqlServer);

        tags.DbName.Should().BeNull();
        tags.DbUser.Should().BeNull();
        tags.OutHost.Should().BeNull();
        tags.DbNamespace.Should().BeNull();
        tags.ServerAddress.Should().BeNull();
        tags.ServerPort.Should().BeNull();
    }

    private static IDbCommand CreateDbCommand(string connectionString)
    {
        var connection = new Mock<IDbConnection>();
        connection.Setup(c => c.ConnectionString).Returns(connectionString);

        var command = new Mock<IDbCommand>();
        command.Setup(c => c.Connection).Returns(connection.Object);

        return command.Object;
    }
}
