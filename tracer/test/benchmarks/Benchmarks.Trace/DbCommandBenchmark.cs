using System;
using System.Data;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent1]
    [BenchmarkCategory(Constants.TracerCategory)]

    public class DbCommandBenchmark
    {
        private static readonly CustomDbCommand CustomCommand = new CustomDbCommand();

        static DbCommandBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            var bench = new DbCommandBenchmark();
            bench.ExecuteNonQuery();
        }

        [Benchmark]
        public unsafe int ExecuteNonQuery()
        {
            return CallTarget.Run<CommandExecuteNonQueryIntegration, CustomDbCommand, int>(CustomCommand, &InternalExecuteNonQuery);

            static int InternalExecuteNonQuery() => 1;
        }

        private class CustomDbCommand : IDbCommand
        {
            public void Dispose()
            {
            }

            public void Prepare()
            {
            }

            public void Cancel()
            {
            }

            public IDbDataParameter CreateParameter() => throw new NotImplementedException();

            public int ExecuteNonQuery()
            {
                return 1;
            }

            public IDataReader ExecuteReader() => throw new NotImplementedException();

            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();

            public object ExecuteScalar() => throw new NotImplementedException();

            public IDbConnection Connection { get; set; } = new CustomDbConnection();
            public IDbTransaction Transaction { get; set; }
            public string CommandText { get; set; } = "SELECT * FROM Table WHERE stuff=1";
            public int CommandTimeout { get; set; }
            public CommandType CommandType { get; set; }
            public IDataParameterCollection Parameters { get; }
            public UpdateRowSource UpdatedRowSource { get; set; }
        }

        private class CustomDbConnection : IDbConnection
        {
            public void Dispose() => throw new NotImplementedException();

            public IDbTransaction BeginTransaction() => throw new NotImplementedException();

            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();

            public void Close() => throw new NotImplementedException();

            public void ChangeDatabase(string databaseName) => throw new NotImplementedException();

            public IDbCommand CreateCommand() => throw new NotImplementedException();

            public void Open() => throw new NotImplementedException();

            public string ConnectionString { get; set; } = "Server=myServerName,myPortNumber;Database=myDataBase;User Id=myUsername;Password=myPassword;";

            public int ConnectionTimeout { get; }

            public string Database { get; }

            public ConnectionState State { get; }
        }
    }
}
