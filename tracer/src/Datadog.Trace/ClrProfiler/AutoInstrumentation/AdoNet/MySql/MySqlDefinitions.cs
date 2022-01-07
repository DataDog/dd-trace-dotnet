// <copyright file="MySqlDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethod(
    AssemblyName = "MySql.Data",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "6.7.0",
    MaximumVersion = "6.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>",
    SignatureAttributes = new[]
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

[assembly: AdoNetClientInstrumentMethod(
    AssemblyName = "MySql.Data",
    TypeName = "MySql.Data.MySqlClient.MySqlCommand",
    MinimumVersion = "8.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySql.Data.MySqlClient.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<MySql.Data.MySqlClient.MySqlDataReader>",
    SignatureAttributes = new[]
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

[assembly: AdoNetClientInstrumentMethod(
    AssemblyName = "MySqlConnector",
    TypeName = "MySqlConnector.MySqlCommand",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.MySql),
    DataReaderType = "MySqlConnector.MySqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<MySqlConnector.MySqlDataReader>",
    SignatureAttributes = new[]
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
