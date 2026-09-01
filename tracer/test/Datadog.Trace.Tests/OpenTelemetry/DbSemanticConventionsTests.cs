// <copyright file="DbSemanticConventionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.OpenTelemetry;
using Datadog.Trace.Tagging;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OpenTelemetry;

public class DbSemanticConventionsTests
{
    public enum SQLiteErrorCode
    {
        Ok = 0,
        Constraint = 19,
    }

    [Theory]
    // The values the specification defines for the providers we instrument
    [InlineData("sql-server", "microsoft.sql_server")]
    [InlineData("postgres", "postgresql")]
    [InlineData("mysql", "mysql")]
    [InlineData("oracle", "oracle.db")]
    [InlineData("sqlite", "sqlite")]

    // Any other ADO.NET provider reports the name we derived from its command type, which the
    // specification allows as a custom value because it is lowercase and version-free
    [InlineData("fake", "fake")]
    [InlineData("duckdb", "duckdb")]

    // Only a provider we could not name at all falls back to "other_sql"
    [InlineData(null, "other_sql")]
    [InlineData("", "other_sql")]
    public void GetDbSystemName_MapsTheDatadogVocabularyToTheOpenTelemetryOne(string dbType, string expected)
    {
        DbSemanticConventions.GetDbSystemName(dbType).Should().Be(expected);
    }

    [Theory]
    // A default instance reports the database alone
    [InlineData("localhost", "customers", "customers")]

    // A named instance is the more general namespace component, so it comes first
    [InlineData("localhost\\instance1", "products", "instance1|products")]
    [InlineData("tcp:localhost\\instance1,1434", "products", "instance1|products")]
    [InlineData("(localdb)\\MSSQLLocalDB", "products", "MSSQLLocalDB|products")]
    [InlineData("np:\\\\localhost\\pipe\\MSSQL$instance1\\sql\\query", "products", "instance1|products")]

    // A missing component is omitted, along with its separator
    [InlineData("localhost\\instance1", null, "instance1")]
    [InlineData("localhost", null, null)]
    public void GetConnectionAttributes_QualifiesTheSqlServerNamespaceWithTheInstanceName(string dataSource, string databaseName, string expected)
    {
        DbSemanticConventions.GetConnectionAttributes("sql-server", dataSource, port: null, databaseName, out _, out _, out var dbNamespace);

        dbNamespace.Should().Be(expected);
    }

    [Theory]
    // SQLite is embedded, so the data source is what identifies the database
    [InlineData(":memory:", null, ":memory:")]
    [InlineData("/tmp/mydb.db", null, "/tmp/mydb.db")]

    // ... unless the connection string names one explicitly
    [InlineData("/tmp/mydb.db", "customers", "customers")]
    public void GetConnectionAttributes_UsesTheDataSourceAsTheSqliteNamespace(string dataSource, string databaseName, string expected)
    {
        DbSemanticConventions.GetConnectionAttributes("sqlite", dataSource, port: null, databaseName, out var serverAddress, out var serverPort, out var dbNamespace);

        dbNamespace.Should().Be(expected);

        // There is no server to report for an embedded database
        serverAddress.Should().BeNull();
        serverPort.Should().BeNull();
    }

    [Theory]
    // The plain forms
    [InlineData("sql-server", "localhost", null, "localhost", null)]
    [InlineData("sql-server", "  localhost  ", null, "localhost", null)]
    [InlineData("sql-server", "", null, null, null)]
    [InlineData("sql-server", null, null, null, null)]

    // The port is only reported when it is not the default of the DBMS
    [InlineData("sql-server", "localhost,1433", null, "localhost", null)]
    [InlineData("sql-server", "localhost,1434", null, "localhost", 1434)]
    [InlineData("postgres", "localhost", "5432", "localhost", null)]
    [InlineData("postgres", "localhost", "5433", "localhost", 5433)]
    [InlineData("mysql", "localhost", "3306", "localhost", null)]
    [InlineData("mysql", "localhost", "3307", "localhost", 3307)]
    [InlineData("oracle", "localhost:1521/XE", null, "localhost", null)]
    [InlineData("oracle", "localhost:1522/XE", null, "localhost", 1522)]

    // A protocol prefix and an instance name belong to neither "server.address" nor "server.port"
    [InlineData("sql-server", "tcp:localhost", null, "localhost", null)]
    [InlineData("sql-server", "tcp:localhost,1434", null, "localhost", 1434)]
    [InlineData("sql-server", "lpc:localhost", null, "localhost", null)]
    [InlineData("sql-server", "localhost\\SQLEXPRESS", null, "localhost", null)]
    [InlineData("sql-server", "localhost\\SQLEXPRESS,1434", null, "localhost", 1434)]
    [InlineData("sql-server", "(localdb)\\MSSQLLocalDB", null, "(localdb)", null)]
    [InlineData("sql-server", "np:\\\\myserver\\pipe\\MSSQL$instance1\\sql\\query", null, "myserver", null)]

    // IPv6 addresses are reported without the brackets that delimit them
    [InlineData("sql-server", "[::1]", null, "::1", null)]
    [InlineData("sql-server", "[::1],1434", null, "::1", 1434)]
    [InlineData("postgres", "[2001:db8::1]:5433", null, "2001:db8::1", 5433)]
    [InlineData("postgres", "::1", null, "::1", null)]

    // The first host of a failover list is the one we connect to first
    [InlineData("postgres", "host1,host2", null, "host1", null)]

    // An unknown provider has no default port to omit
    [InlineData("fake", "localhost", "1433", "localhost", 1433)]
    [InlineData("fake", "localhost:1234", null, "localhost", 1234)]
    public void GetConnectionAttributes_ReportsTheServerAddressAndNonDefaultPort(string dbType, string dataSource, string port, string expectedAddress, int? expectedPort)
    {
        DbSemanticConventions.GetConnectionAttributes(dbType, dataSource, port, databaseName: null, out var serverAddress, out var serverPort, out _);

        serverAddress.Should().Be(expectedAddress);
        serverPort.Should().Be(expectedPort);
    }

    [Theory]
    // The "easy connect" forms
    [InlineData("//localhost:1522/XE", "localhost", 1522, "XE")]
    [InlineData("localhost/XE", "localhost", null, "XE")]

    // A TNS connect descriptor
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracledb)(PORT=1522))(CONNECT_DATA=(SERVICE_NAME=ORCL)))", "oracledb", 1522, "ORCL")]
    [InlineData("(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=oracledb)(PORT=1521))(CONNECT_DATA=(SID=ORCL)))", "oracledb", null, "ORCL")]

    // A bare TNS alias names neither a host nor a service
    [InlineData("ORCL_ALIAS", "ORCL_ALIAS", null, null)]
    public void GetConnectionAttributes_ParsesTheOracleDataSource(string dataSource, string expectedAddress, int? expectedPort, string expectedNamespace)
    {
        DbSemanticConventions.GetConnectionAttributes("oracle", dataSource, port: null, databaseName: null, out var serverAddress, out var serverPort, out var dbNamespace);

        serverAddress.Should().Be(expectedAddress);
        serverPort.Should().Be(expectedPort);
        dbNamespace.Should().Be(expectedNamespace);
    }

    [Fact]
    public void GetConnectionAttributes_PrefersTheDatabaseNameOverTheOracleServiceName()
    {
        DbSemanticConventions.GetConnectionAttributes("oracle", "//localhost/XE", port: null, databaseName: "customers", out _, out _, out var dbNamespace);

        dbNamespace.Should().Be("customers");
    }

    [Fact]
    public void SetDbClientRequestValues_ReportsTheSanitizedQueryTextForATextCommand()
    {
        var span = CreateSpan();
        var tags = new SqlTags { DbType = "postgresql", DbName = "customers" };

        DbSemanticConventions.SetDbClientRequestValues(span, tags, CommandType.Text, "SELECT * FROM users WHERE name = 'zach' AND id = 12");

        tags.DbQueryText.Should().Be("SELECT * FROM users WHERE name = ? AND id = ?");

        // There is no operation, collection, or summary without a SQL parser
        tags.DbOperationName.Should().BeNull();
        tags.DbCollectionName.Should().BeNull();
        tags.DbStoredProcedureName.Should().BeNull();
        tags.DbQuerySummary.Should().BeNull();
    }

    [Theory]
    // Microsoft SQL Server does not support the SQL standard CALL keyword
    [InlineData("microsoft.sql_server", "EXECUTE")]
    [InlineData("postgresql", "CALL")]
    [InlineData("mysql", "CALL")]
    public void SetDbClientRequestValues_ReportsTheProcedureNameForAStoredProcedure(string dbSystemName, string expectedOperation)
    {
        var span = CreateSpan();
        var tags = new SqlTags { DbType = dbSystemName, DbName = "customers" };

        DbSemanticConventions.SetDbClientRequestValues(span, tags, CommandType.StoredProcedure, "get_customer");

        tags.DbOperationName.Should().Be(expectedOperation);
        tags.DbStoredProcedureName.Should().Be("get_customer");
        tags.DbQuerySummary.Should().Be($"{expectedOperation} get_customer");
        span.ResourceName.Should().Be($"{expectedOperation} get_customer");

        // The command text is the name of the procedure, not a query
        tags.DbQueryText.Should().BeNull();
    }

    [Fact]
    public void SetDbClientRequestValues_ReportsTheTableNameForATableDirectCommand()
    {
        var span = CreateSpan();
        var tags = new SqlTags { DbType = "microsoft.sql_server", DbName = "customers" };

        DbSemanticConventions.SetDbClientRequestValues(span, tags, CommandType.TableDirect, "users");

        tags.DbOperationName.Should().Be("SELECT");
        tags.DbCollectionName.Should().Be("users");
        tags.DbQuerySummary.Should().Be("SELECT users");
        span.ResourceName.Should().Be("SELECT users");
        tags.DbQueryText.Should().BeNull();
    }

    [Theory]
    // The query summary wins when there is one
    [InlineData("microsoft.sql_server", "EXECUTE get_customer", null, null, null, null, "EXECUTE get_customer")]

    // ... then "{db.operation.name} {target}", with the collection preferred over the namespace
    [InlineData("microsoft.sql_server", null, "SELECT", "users", "customers", null, "SELECT users")]
    [InlineData("microsoft.sql_server", null, "SELECT", null, "customers", null, "SELECT customers")]

    // ... then the target alone: the namespace, then the server address and port
    [InlineData("microsoft.sql_server", null, null, null, "customers", null, "customers")]
    [InlineData("microsoft.sql_server", null, null, null, null, "localhost", "localhost:1434")]

    // ... and finally the database management system
    [InlineData("microsoft.sql_server", null, null, null, null, null, "microsoft.sql_server")]
    public void GetSpanName_FollowsTheSpecificationsPrecedence(
        string dbSystemName,
        string querySummary,
        string operationName,
        string collectionName,
        string dbNamespace,
        string serverAddress,
        string expected)
    {
        var tags = new SqlTags
        {
            DbType = dbSystemName,
            DbQuerySummary = querySummary,
            DbOperationName = operationName,
            DbCollectionName = collectionName,
            DbName = dbNamespace,
            OutHost = serverAddress,
            ServerPort = serverAddress is null ? null : 1434,
        };

        DbSemanticConventions.GetSpanName(tags).Should().Be(expected);
    }

    [Fact]
    public void GetSpanName_OmitsThePortWhenItIsTheDefaultOfTheDbms()
    {
        // "server.port" is unset when the default port is in use, so the target is the address alone
        var tags = new SqlTags { DbType = "microsoft.sql_server", OutHost = "localhost" };

        DbSemanticConventions.GetSpanName(tags).Should().Be("localhost");
    }

    [Fact]
    public void SetDbClientRequestValues_KeepsTheCommandTextForTheNestedCommandCheck()
    {
        const string commandText = "SELECT * FROM users WHERE id = 12";
        var span = CreateSpan();
        var tags = new SqlTags { DbType = "microsoft.sql_server", DbName = "customers" };

        DbSemanticConventions.SetDbClientRequestValues(span, tags, CommandType.Text, commandText);

        // The resource name is the low-cardinality span name, so the command is recognized through
        // the text we kept rather than through the resource name
        span.ResourceName.Should().Be("customers");
        DbSemanticConventions.IsSameCommand(tags, commandText).Should().BeTrue();
        DbSemanticConventions.IsSameCommand(tags, "SELECT * FROM users WHERE id = 34").Should().BeFalse();
    }

    [Fact]
    public void SetDbClientErrorValues_ReportsTheCanonicalExceptionTypeName()
    {
        var tags = new SqlTags();

        DbSemanticConventions.SetDbClientErrorValues(tags, new InvalidOperationException("nope"));

        tags.ErrorType.Should().Be("System.InvalidOperationException");
        tags.DbResponseStatusCode.Should().BeNull();
    }

    [Fact]
    public void SetDbClientErrorValues_UnwrapsAnAggregateException()
    {
        var tags = new SqlTags();

        DbSemanticConventions.SetDbClientErrorValues(tags, new AggregateException(new TimeoutException("nope")));

        tags.ErrorType.Should().Be("System.TimeoutException");
    }

    [Theory]
    // A vendor-specific code is preferred over SQLSTATE, as the specification asks
    [InlineData(typeof(NumberException), "1205")]
    [InlineData(typeof(SqlStateException), "08P01")]
    [InlineData(typeof(SqliteErrorCodeException), "19")]
    [InlineData(typeof(ResultCodeException), "Constraint")]
    [InlineData(typeof(NumberAndSqlStateException), "1071")]
    public void GetResponseStatusCode_ReadsTheMostSpecificCodeTheProviderExposes(Type exceptionType, string expected)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType);

        DbSemanticConventions.GetResponseStatusCode(exception).Should().Be(expected);
    }

    [Fact]
    public void GetResponseStatusCode_IgnoresAPropertyGetterThatThrows()
    {
        DbSemanticConventions.GetResponseStatusCode(new ThrowingNumberException()).Should().BeNull();
    }

    private static Span CreateSpan()
        => new(new SpanContext(1, 1), DateTimeOffset.UtcNow, new SqlTags());

    // Stand-ins for the provider exception types, which the tracer cannot reference. Each one
    // exposes the status-code property of a real provider, so the reflection lookup is exercised
    // exactly as it would be at runtime.
    public class NumberException : Exception
    {
        public int Number => 1205;
    }

    public class SqlStateException : Exception
    {
        public string SqlState => "08P01";
    }

    public class SqliteErrorCodeException : Exception
    {
        public int SqliteErrorCode => 19;
    }

    public class ResultCodeException : Exception
    {
        public SQLiteErrorCode ResultCode => SQLiteErrorCode.Constraint;
    }

    public class NumberAndSqlStateException : Exception
    {
        public int Number => 1071;

        public string SqlState => "42000";
    }

    public class ThrowingNumberException : Exception
    {
        public int Number => throw new NotSupportedException();
    }
}
