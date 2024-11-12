// <copyright file="MySqlDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySql.Data",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "6.7.0",
    MaximumVersion = "6.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySql.Data.MySqlClient.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object MySql.Data.MySqlClient.MySqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySql.Data",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "8.0.0",
    MaximumVersion = "9.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySql.Data.MySqlClient.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<MySqlDataReader> MySql.Data.MySqlClient.MySqlCommand.ExecuteReaderAsync(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAsyncAttribute),
        // Task<MySqlDataReader> MySql.Data.MySqlClient.MySqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<MySqlDataReader> MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> MySql.Data.MySqlClient.MySqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object MySql.Data.MySqlClient.MySqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySqlConnector",
    TypeName = "MySqlConnector.MySqlCommand",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySqlConnector.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySqlConnector.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> MySqlConnector.MySqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int MySqlConnector.MySqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<MySqlDataReader> MySqlConnector.MySqlCommand.ExecuteReaderAsync(CancellationToken)
        typeof(CommandExecuteReaderWithCancellationAsyncAttribute),
        // Task<MySqlDataReader> MySqlConnector.MySqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> MySqlConnector.MySqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // MySqlDataReader MySqlConnector.MySqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // MySqlDataReader MySqlConnector.MySqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader MySqlConnector.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> MySqlConnector.MySqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object MySqlConnector.MySqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "MySqlConnector",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "0.61.0",
    MaximumVersion = "0.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[MySql.Data.MySqlClient.MySqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<DbDataReader> MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> MySql.Data.MySqlClient.MySqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object MySql.Data.MySqlClient.MySqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]
