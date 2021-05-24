// <copyright file="SqliteDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Sqlite.SqliteConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int Microsoft.Data.Sqlite.SqliteCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(MicrosoftDataSqliteClientData))]

// int System.Data.SQLite.SQLiteCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<SqliteDataReader> Microsoft.Data.Sqlite.SqliteCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteReaderWithBehaviorAsync(typeof(MicrosoftDataSqliteClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAsync(typeof(MicrosoftDataSqliteClientData))]

/********************************************************************************
 * [*]DataReader .ExecuteReader()
 ********************************************************************************/

// SqliteDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(MicrosoftDataSqliteClientData))]

// SQLiteDataReader System.Data.SQLite.SQLiteCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteReader(CommandBehavior)
 ********************************************************************************/

// SqliteDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(MicrosoftDataSqliteClientData))]

// SQLiteDataReader System.Data.SQLite.SQLiteCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader Microsoft.Data.Sqlite.SqliteCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(MicrosoftDataSqliteClientData))]

// DbDataReader System.Data.SQLite.SQLiteCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object Microsoft.Data.Sqlite.SqliteCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(MicrosoftDataSqliteClientData))]

// object System.Data.SQLite.SQLiteCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * object .ExecuteScalar(CommandBehavior)
 ********************************************************************************/

// object System.Data.SQLite.SQLiteCommand.ExecuteScalar(CommandBehavior)
[assembly: CommandExecuteScalarWithBehavior(typeof(SystemDataSqliteClientData))]

/********************************************************************************
 * int .ExecuteNonQuery(CommandBehavior)
 ********************************************************************************/

// int System.Data.SQLite.SQLiteCommand.ExecuteNonQuery(CommandBehavior)
[assembly: CommandExecuteNonQueryWithBehavior(typeof(SystemDataSqliteClientData))]
