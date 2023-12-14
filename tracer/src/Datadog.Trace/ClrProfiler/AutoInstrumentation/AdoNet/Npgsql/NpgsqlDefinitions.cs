// <copyright file="NpgsqlDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Npgsql",
    TypeName = "Npgsql.NpgsqlCommand",
    MinimumVersion = "4.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = nameof(IntegrationId.Npgsql),
    DataReaderType = "Npgsql.NpgsqlDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Npgsql.NpgsqlDataReader]",
    TargetMethodAttributes = new[]
    {
        // Task<int> Npgsql.NpgsqlCommand.ExecuteNonQueryAsync(CancellationToken)
        typeof(CommandExecuteNonQueryAsyncAttribute),
        // int Npgsql.NpgsqlCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<NpgsqlDataReader> Npgsql.NpgsqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // NpgsqlDataReader Npgsql.NpgsqlCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // NpgsqlDataReader Npgsql.NpgsqlCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Npgsql.NpgsqlCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // Task<object> Npgsql.NpgsqlCommand.ExecuteScalarAsync(CancellationToken)
        typeof(CommandExecuteScalarAsyncAttribute),
        // object Npgsql.NpgsqlCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]
