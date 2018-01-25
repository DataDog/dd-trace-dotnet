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

        public SqlClientListenerTests()
        {
            _sqlListener = new SqlClientListener(Tracer.Instance);
            _globalListener = new GlobalListener(SqlClientListenerName, _sqlListener);
        }

        [Fact]
        public void Test1()
        {
            using (var connection = new SqlConnection())
            {
                SqlCommand command = new SqlCommand("Select * from lol", connection);

                // connection.Open();
                var reader = command.ExecuteReader();
                reader.Close();
            }
        }
    }
}
