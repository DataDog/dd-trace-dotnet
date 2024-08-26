// <copyright file="SqliteDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

#pragma warning disable SA1118 // parameter spans multiple lines
[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Microsoft.Data.Sqlite",
    TypeName = "Microsoft.Data.Sqlite.SqliteCommand",
    MinimumVersion = "2.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = nameof(IntegrationId.Sqlite),
    DataReaderType = "Microsoft.Data.Sqlite.SqliteDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[Microsoft.Data.Sqlite.SqliteDataReader]",
    TargetMethodAttributes = new[]
    {
        // int Microsoft.Data.Sqlite.SqliteCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // Task<SqliteDataReader> Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteReaderWithBehaviorAndCancellationAsyncAttribute),
        // Task<DbDataReader> Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
        typeof(CommandExecuteDbDataReaderWithBehaviorAndCancellationAsyncAttribute),
        // SqliteDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // SqliteDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object Microsoft.Data.Sqlite.SqliteCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "System.Data.SQLite",
    TypeName = "System.Data.SQLite.SQLiteCommand",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.Sqlite),
    DataReaderType = "System.Data.SQLite.SqliteDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.SQLite.SqliteDataReader]",
    TargetMethodAttributes = new[]
    {
        // int System.Data.SQLite.SQLiteCommand.ExecuteNonQuery()
        typeof(CommandExecuteNonQueryAttribute),
        // SQLiteDataReader System.Data.SQLite.SQLiteCommand.ExecuteReader()
        typeof(CommandExecuteReaderAttribute),
        // SQLiteDataReader System.Data.SQLite.SQLiteCommand.ExecuteReader(CommandBehavior)
        typeof(CommandExecuteReaderWithBehaviorAttribute),
        // DbDataReader System.Data.SQLite.SQLiteCommand.ExecuteDbDataReader(CommandBehavior)
        typeof(CommandExecuteDbDataReaderWithBehaviorAttribute),
        // object System.Data.SQLite.SQLiteCommand.ExecuteScalar()
        typeof(CommandExecuteScalarAttribute),
        // object System.Data.SQLite.SQLiteCommand.ExecuteScalar(CommandBehavior)
        typeof(CommandExecuteScalarWithBehaviorAttribute),
        // int System.Data.SQLite.SQLiteCommand.ExecuteNonQuery(CommandBehavior)
        typeof(CommandExecuteNonQueryWithBehaviorAttribute),
    })]

[assembly: AdoNetClientInstrumentMethods(
    AssemblyName = "Microsoft.Data.Sqlite",
    TypeName = "Microsoft.Data.Sqlite.SQLiteDataReader",
    MinimumVersion = "2.0.0",
    MaximumVersion = "8.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = "System.Data.SQLite.SQLiteDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.SQLite.SQLiteDataReader]",
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
    AssemblyName = "System.Data.SQLite",
    TypeName = "System.Data.SQLite.SQLiteDataReader",
    MinimumVersion = "1.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.SqlClient),
    DataReaderType = "System.Data.SQLite.SQLiteDataReader",
    DataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.SQLite.SQLiteDataReader]",
    TargetMethodAttributes = new[]
    {
        // string System.Data.Common.DbDataReader.GetString()
        typeof(ReaderReadAttribute),
        typeof(ReaderReadAsyncAttribute),
        typeof(ReaderCloseAttribute),
        typeof(ReaderGetStringAttribute),
        typeof(ReaderGetValueAttribute),
    })]
