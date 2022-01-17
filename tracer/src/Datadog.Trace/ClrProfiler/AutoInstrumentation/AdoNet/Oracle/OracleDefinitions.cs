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
    MinimumVersion = "4.122.0",
    MaximumVersion = "4.122.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<Oracle.ManagedDataAccess.Client.OracleDataReader>",
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
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.ManagedDataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<Oracle.ManagedDataAccess.Client.OracleDataReader>",
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
    AssemblyName = "Oracle.DataAccess",
    TypeName = "Oracle.DataAccess.Client.OracleCommand",
    MinimumVersion = "4.122.0",
    MaximumVersion = "4.122.*",
    IntegrationName = nameof(IntegrationId.Oracle),
    DataReaderType = "Oracle.DataAccess.Client.OracleDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1<Oracle.DataAccess.Client.OracleDataReader>",
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
