using System;
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
        }
    }
}
