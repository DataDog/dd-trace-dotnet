// <copyright file="SqlClientDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetConstants;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data",
    TypeName = "System.Data.SqlClient.SqlCommand",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = "System.Data.SqlClient.SqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.SqlClient.SqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> System.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int System.Data.SqlClient.SqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<SqlDataReader> System.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> System.Data.SqlClient.SqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader System.Data.SqlClient.SqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> System.Data.SqlClient.SqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object System.Data.SqlClient.SqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data.SqlClient",
    TypeName = "System.Data.SqlClient.SqlCommand",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = "System.Data.SqlClient.SqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.SqlClient.SqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> System.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int System.Data.SqlClient.SqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<SqlDataReader> System.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> System.Data.SqlClient.SqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader System.Data.SqlClient.SqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> System.Data.SqlClient.SqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object System.Data.SqlClient.SqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Microsoft.Data.SqlClient",
    TypeName = "Microsoft.Data.SqlClient.SqlCommand",
    MinimumVersion = "1.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = "Microsoft.Data.SqlClient.SqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Microsoft.Data.SqlClient.SqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync()
        typeof(CommandExecuteReaderAsyncAttribute),
        // Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CancellationToken)
        typeof(CommandExecuteReaderWithCancellationAsyncAttribute),
        // Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAsyncAttribute),
        // Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // SqlDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // SqlDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> Microsoft.Data.SqlClient.SqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object Microsoft.Data.SqlClient.SqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data",
    TypeName = "System.Data.SqlClient.SqlDataReader",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // string System.Data.Common.DbDataReader.GetString()
        typeof(ReaderReadAttribute),
        typeof(ReaderReadAsyncAttribute),
        typeof(ReaderCloseAttribute),
        typeof(ReaderGetStringAttribute),
        typeof(ReaderGetValueAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data.SqlClient",
    TypeName = "System.Data.SqlClient.SqlDataReader",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // string System.Data.Common.DbDataReader.GetString()
        typeof(ReaderReadAttribute),
        typeof(ReaderReadAsyncAttribute),
        typeof(ReaderCloseAttribute),
        typeof(ReaderGetStringAttribute),
        typeof(ReaderGetValueAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Microsoft.Data.SqlClient",
    TypeName = "Microsoft.Data.SqlClient.SqlDataReader",
    MinimumVersion = "1.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // string System.Data.Common.DbDataReader.GetString()
        typeof(ReaderReadAttribute),
        typeof(ReaderReadAsyncAttribute),
        typeof(ReaderCloseAttribute),
        typeof(ReaderGetStringAttribute),
        typeof(ReaderGetValueAttribute),
    })]
