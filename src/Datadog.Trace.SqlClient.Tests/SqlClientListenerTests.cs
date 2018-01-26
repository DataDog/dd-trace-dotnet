using System;
using System.Data.SqlClient;
using Xunit;

namespace Datadog.Trace.SqlClient.Tests
{
    public class SqlClientListenerTests
    {
        private const string SqlClientListenerName = "SqlClientDiagnosticListener";
        private static GlobalListener _globalListener;
        private static SqlClientListener _sqlListener;

        private string connectionString = "Server=172.29.108.68;User Id=SA;Password=pass";

        //// Provide the query string with a parameter placeholder.
        // private string queryString =
        //    "SELECT * from dbo.employee";
        public SqlClientListenerTests()
        {
            _sqlListener = new SqlClientListener(Tracer.Instance);
            _globalListener = new GlobalListener(SqlClientListenerName, _sqlListener);
        }

        [Fact]
        public void Test1()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand("Select * from lol", connection);

                connection.Open();
                var reader = command.ExecuteReader();
                reader.Close();
            }
        }
    }
}
