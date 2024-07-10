// <copyright file="OracleDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Oracle.ManagedDataAccess",
    TypeName = "Oracle.ManagedDataAccess.Client.OracleCommand",
    // The netframework nuget depends on the v4.122.* of the dll.
    // It seems that the pattern is 4.122.<major version of the nuget>.
    // For netcore, the major version of the dll matches the major version of the nuget,
    // that was v2 and v3, but they have recently bumped it from 3 to 23
    // (to have matching version numbers between netcore and netframework I suppose).
    // here we target the older versions of the netcore dll, and the netframework.
    // instrumentation for v23 is below, separated to make sure that we don't instrument a hypothetical v5 by mistake.
    MinimumVersion = "2.0.0",
    MaximumVersion = "4.122.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Oracle.ManagedDataAccess.Client.OracleDataReader]",
    TargetMethodAttributes = new[]
    {
        // int Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Oracle.ManagedDataAccess",
    TypeName = "Oracle.ManagedDataAccess.Client.OracleCommand",
    // see comment above on version numbers
    MinimumVersion = "23.0.0",
    MaximumVersion = "23.*.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Oracle.ManagedDataAccess.Client.OracleDataReader]",
    TargetMethodAttributes = new[]
    {
        // int Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute)
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Oracle.DataAccess",
    TypeName = "Oracle.DataAccess.Client.OracleCommand",
    MinimumVersion = "4.122.0",
    MaximumVersion = "4.122.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.DataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Oracle.DataAccess.Client.OracleDataReader]",
    TargetMethodAttributes = new[]
    {
        // int Oracle.DataAccess.Client.OracleCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // OracleDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // OracleDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object Oracle.DataAccess.Client.OracleCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]
