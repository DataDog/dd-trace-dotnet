using System;
using System.Data;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations.AdoNet;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class DbCommandBenchmark
    {
        private static readonly int MdToken;
        private static readonly IntPtr GuidPtr;
        private static readonly IDbCommand DbCommand = new CustomDbCommand();

        static DbCommandBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var methodInfo = typeof(IDbCommand).GetMethod("ExecuteNonQuery", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            MdToken = methodInfo.MetadataToken;
            var guid = typeof(IDbCommand).Module.ModuleVersionId;

            GuidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, GuidPtr, false);

            new DbCommandBenchmark().ExecuteNonQuery();
        }

        [Benchmark]
        public int ExecuteNonQuery()
        {
            return IDbCommandIntegration.ExecuteNonQuery(DbCommand, (int)OpCodeValue.Callvirt, MdToken, (long)GuidPtr);
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
