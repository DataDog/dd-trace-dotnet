// <copyright file="AdoNetDefinitions.cs" company="Datadog">
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
    TypeName = "System.Data.Common.DbCommand",
    MinimumVersion = "4.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.AdoNet),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // Task<int> System.Data.Common.DbCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // Task<DbDataReader> System.Data.Common.DbCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<object> System.Data.Common.DbCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data.Common",
    TypeName = "System.Data.Common.DbCommand",
    MinimumVersion = "4.0.0",
    MaximumVersion = "7.*.*",
    IntegrationName = nameof(IntegrationId.AdoNet),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // Task<int> System.Data.Common.DbCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // Task<DbDataReader> System.Data.Common.DbCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<object> System.Data.Common.DbCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // int System.Data.Common.DbCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryDerivedAttribute),
        // object System.Data.Common.DbCommand.ExecuteScalar()
        typeof(CommandExecuteScalarDerivedAttribute),
        // DbDataReader System.Data.Common.DbCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorDerivedAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data",
    TypeName = "System.Data.Common.DbCommand",
    MinimumVersion = "2.0.0",
    MaximumVersion = "4.*.*",
    IntegrationName = nameof(IntegrationId.AdoNet),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // int System.Data.Common.DbCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryDerivedAttribute),
        // object System.Data.Common.DbCommand.ExecuteScalar()
        typeof(CommandExecuteScalarDerivedAttribute),
        // DbDataReader System.Data.Common.DbCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorDerivedAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "netstandard",
    TypeName = "System.Data.Common.DbCommand",
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.AdoNet),
    DataReaderType = TypeNames.DbDataReaderType,
    DataReaderTaskType = TypeNames.DbDataReaderTaskType,
    TargetMethodAttributes = new[]
    {
        // int System.Data.Common.DbCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryDerivedAttribute),
        // object System.Data.Common.DbCommand.ExecuteScalar()
        typeof(CommandExecuteScalarDerivedAttribute),
        // DbDataReader System.Data.Common.DbCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorDerivedAttribute),
    })]
