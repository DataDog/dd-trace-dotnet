// <copyright file="OracleActionBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.DatabaseMonitoring;

namespace Benchmarks.Trace
{
    /// <summary>
    /// Cost of writing the DBM service hash to the Oracle session ACTION before each execution
    /// (DD_DBM_PROPAGATION_MODE=dynamic_service with Oracle).
    /// </summary>
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class OracleActionBenchmark
    {
        private const string BaseHash = "_ix0u4wQkFQ";

        private OracleCommand _command;

        [GlobalSetup]
        public void GlobalSetup()
        {
            _command = new OracleCommand { Connection = new OracleConnection() };

            // warm up the duck type cache
            DatabaseMonitoringPropagator.PropagateServiceHashViaOracleAction(_command, BaseHash);
        }

        /// <summary>
        /// Several commands executed on the same connection object.
        /// </summary>
        [Benchmark]
        public bool SameConnection()
        {
            return DatabaseMonitoringPropagator.PropagateServiceHashViaOracleAction(_command, BaseHash);
        }

        /// <summary>
        /// One command per connection object, as with <c>using var connection = new OracleConnection(...)</c> per unit of work.
        /// The connection allocation is included in this measurement.
        /// </summary>
        [Benchmark]
        public bool NewConnectionEachCall()
        {
            _command.Connection = new OracleConnection();
            return DatabaseMonitoringPropagator.PropagateServiceHashViaOracleAction(_command, BaseHash);
        }

        private class OracleConnection : IDbConnection
        {
            private string _actionName;

            public string ConnectionString { get; set; }

            public int ConnectionTimeout => 0;

            public string Database => string.Empty;

            public ConnectionState State => ConnectionState.Open;

            // ODP.NET exposes ActionName as a write-only property
            public string ActionName
            {
                set => _actionName = value;
            }

            public IDbTransaction BeginTransaction() => throw new NotImplementedException();

            public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotImplementedException();

            public void ChangeDatabase(string databaseName) => throw new NotImplementedException();

            public void Close()
            {
            }

            public IDbCommand CreateCommand() => throw new NotImplementedException();

            public void Open()
            {
            }

            public void Dispose()
            {
            }
        }

        private class OracleCommand : IDbCommand
        {
            public string CommandText { get; set; } = "SELECT 1 FROM DUAL";

            public int CommandTimeout { get; set; }

            public CommandType CommandType { get; set; }

            public IDbConnection Connection { get; set; }

            public IDataParameterCollection Parameters => throw new NotImplementedException();

            public IDbTransaction Transaction { get; set; }

            public UpdateRowSource UpdatedRowSource { get; set; }

            public void Cancel()
            {
            }

            public IDbDataParameter CreateParameter() => throw new NotImplementedException();

            public int ExecuteNonQuery() => 0;

            public IDataReader ExecuteReader() => throw new NotImplementedException();

            public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();

            public object ExecuteScalar() => null;

            public void Prepare()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
