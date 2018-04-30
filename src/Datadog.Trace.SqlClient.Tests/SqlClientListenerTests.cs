using System;
using System.Data.SqlClient;
using System.Linq;
using Datadog.Trace.TestUtils;
using Xunit;

namespace Datadog.Trace.SqlClient.Tests
{
    public class SqlClientListenerTests : IDisposable
    {
        private const string InitConnectionString = @"Server=(localdb)\MSSQLLocalDB;Integrated Security=true";
        private const string ConnectionString = InitConnectionString + ";Database=Test";
        private const string SqlRowsTag = "sql.rows";
        private const string SqlQueryTag = "sql.query";
        private const string SqlDatabaseTag = "sql.db";
        private const string SqlExceptionNumberTag = "sql.exceptionNumber";
        private const string ErrorMsgTag = "error.msg";
        private const string ErrorTypeTag = "error.type";
        private const string ErrorStackTag = "error.stack";
        private const string SqlSpanType = "sql";
        private const string DatabaseName = "Test";
        private const string DefaultServiceName = "mssql";

        private static readonly string[] _initDb = new string[]
        {
@"USE master",
@"IF NOT EXISTS(
    SELECT name
        FROM sys.databases
        WHERE name = N'Test'
)
CREATE DATABASE Test",
@"USE Test",
@"IF OBJECT_ID('dbo.Persons', 'U') IS NOT NULL
DROP TABLE dbo.Persons",
@"CREATE TABLE dbo.Persons
(
Id INT IDENTITY(1,1) PRIMARY KEY,
FirstName [NVARCHAR](50) NOT NULL,
LastName [NVARCHAR](50) NOT NULL
);",
@"INSERT INTO dbo.Persons
(
 [FirstName], [LastName]
)
VALUES
(
 'John', 'Galt'
),
(
 'Constantin', 'Levine'
),
(
 'Michel', 'Djerzinski'
)"
        };

        private readonly MockWriter _writer;
        private readonly Tracer _tracer;

        public SqlClientListenerTests()
        {
            using (var connection = new SqlConnection(InitConnectionString))
            {
                connection.Open();
                foreach (var commandText in _initDb)
                {
                    SqlCommand command = new SqlCommand(commandText, connection);
                    command.ExecuteNonQuery();
                }
            }

            _writer = new MockWriter();
            _tracer = new Tracer(_writer);
            SqlClientIntegration.Enable(_tracer);
        }

        public void Dispose()
        {
            SqlClientIntegration.Disable();
        }

        [Fact]
        public void SelectQuery()
        {
            const string query = "SELECT * FROM dbo.Persons;";
            using (var connection = new SqlConnection(ConnectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                var reader = command.ExecuteReader();
                reader.Close();
            }

            var span = _writer.Traces.Single().Single();
#if NETCOREAPP2_0
            Assert.Equal(query, span.ResourceName);

            // "sql.query" should not be set otherwise the query will not be anonymized
            Assert.Null(span.GetTag(SqlQueryTag));
#endif
            Assert.Equal(DatabaseName, span.GetTag(SqlDatabaseTag));
            Assert.Equal(SqlSpanType, span.Type);
            Assert.False(span.Error);
            Assert.Equal(DefaultServiceName, span.ServiceName);
        }

        [Fact]
        public void SelectOnNonExistingDatabase()
        {
            const string query = "SELECT * FROM dbo.Perso;";
            using (var connection = new SqlConnection(ConnectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                try
                {
                    var reader = command.ExecuteReader();
                }
                catch
                {
                }
            }

            var span = _writer.Traces.Single().Single();
#if NETCOREAPP2_0
            Assert.Equal(typeof(SqlException).ToString(), span.GetTag(ErrorTypeTag));
            Assert.Equal("Invalid object name 'dbo.Perso'.", span.GetTag(ErrorMsgTag));
            Assert.False(string.IsNullOrEmpty(span.GetTag(ErrorStackTag)));
            Assert.Equal(query, span.ResourceName);

            // "sql.query" should not be set otherwise the query will not be anonymized
            Assert.Null(span.GetTag(SqlQueryTag));
#else
            Assert.NotNull(span.GetTag(SqlExceptionNumberTag));
#endif
            Assert.Equal(DatabaseName, span.GetTag(SqlDatabaseTag));
            Assert.Equal(SqlSpanType, span.Type);
            Assert.True(span.Error);
            Assert.Equal(DefaultServiceName, span.ServiceName);
        }

        [Fact]
        public void SelectQueryWithServiceNameOverride()
        {
            const string customServiceName = "CustomServiceName";
            const string query = "SELECT * FROM dbo.Persons;";

            SqlClientIntegration.Disable();
            SqlClientIntegration.Enable(_tracer, customServiceName);
            using (var connection = new SqlConnection(ConnectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                var reader = command.ExecuteReader();
                reader.Close();
            }

            var span = _writer.Traces.Single().Single();
#if NETCOREAPP2_0
            Assert.Equal(query, span.ResourceName);

            // "sql.query" should not be set otherwise the query will not be anonymized
            Assert.Null(span.GetTag(SqlQueryTag));
#endif
            Assert.Equal(DatabaseName, span.GetTag(SqlDatabaseTag));
            Assert.Equal(SqlSpanType, span.Type);
            Assert.False(span.Error);
            Assert.Equal(customServiceName, span.ServiceName);
        }
    }
}
