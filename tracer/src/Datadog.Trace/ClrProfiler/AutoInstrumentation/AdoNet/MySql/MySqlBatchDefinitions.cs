// <copyright file="MySqlBatchDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySqlConnector",
    TypeName = "MySql.Data.MySqlClient.MySqlBatch", // legacy type
    MinimumVersion = "0.61.0", // Batch support was added in 0.57, but 0.61 is the earliest we instrument & test
    MaximumVersion = "0.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySql.Data.MySqlClient.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // protected DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        typeof(BatchExecuteDbDataReaderWithBehaviorAttribute),
        // protected Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        typeof(BatchExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // public int ExecuteNonQuery()
        typeof(BatchExecuteNonQueryAttribute),
        // public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteNonQueryAsyncAttribute),
        // public MySqlDataReader ExecuteReader(CommandBehavior behavior = null)
        typeof(BatchExecuteReaderWithBehaviorAttribute),
        // public Task<MySqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteReaderWithCancellationAsyncAttribute),
        // public Task<MySqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = null)
        typeof(BatchExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // public object? ExecuteScalar()
        typeof(BatchExecuteScalarAttribute),
        // public Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteScalarAsyncAttribute)
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySqlConnector",
    TypeName = "MySqlConnector.MySqlBatch",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySqlConnector.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySqlConnector.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // protected DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        typeof(BatchExecuteDbDataReaderWithBehaviorAttribute),
        // protected Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        typeof(BatchExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // public int ExecuteNonQuery()
        typeof(BatchExecuteNonQueryAttribute),
        // public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteNonQueryAsyncAttribute),
        // public MySqlDataReader ExecuteReader(CommandBehavior behavior = null)
        typeof(BatchExecuteReaderWithBehaviorAttribute),
        // public Task<MySqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteReaderWithCancellationAsyncAttribute),
        // public Task<MySqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken = null)
        typeof(BatchExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // public object? ExecuteScalar()
        typeof(BatchExecuteScalarAttribute),
        // public Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken = null)
        typeof(BatchExecuteScalarAsyncAttribute)
    })]
