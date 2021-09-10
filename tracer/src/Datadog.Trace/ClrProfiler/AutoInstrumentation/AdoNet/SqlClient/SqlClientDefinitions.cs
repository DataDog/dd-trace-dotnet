// <copyright file="SqlClientDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.SqlClient.SqlClientConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

// Task<int> System.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataSqlClientAdoNetClientData))]

// Task<int> Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int System.Data.SqlClient.SqlCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteNonQuery(typeof(SystemDataSqlClientAdoNetClientData))]

// int Microsoft.Data.SqlClient.SqlCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync()
 ********************************************************************************/

// Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync()
[assembly: CommandExecuteReaderAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CancellationToken)
 ********************************************************************************/

// Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CancellationToken)
[assembly: CommandExecuteReaderWithCancellationAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior)
 ********************************************************************************/

// Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior)
[assembly: CommandExecuteReaderWithBehaviorAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<SqlDataReader> System.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteReaderWithBehaviorAndCancellationAsync(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteReaderWithBehaviorAndCancellationAsync(typeof(SystemDataSqlClientAdoNetClientData))]

// Task<SqlDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteReaderWithBehaviorAndCancellationAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> System.Data.SqlClient.SqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataSqlClientAdoNetClientData))]

// Task<DbDataReader> Microsoft.Data.SqlClient.SqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * [*]DataReader .ExecuteReader()
 ********************************************************************************/

// SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteReader(typeof(SystemDataSqlClientAdoNetClientData))]

// SqlDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteReader(CommandBehavior)
 ********************************************************************************/

// SqlDataReader System.Data.SqlClient.SqlCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteReaderWithBehavior(typeof(SystemDataSqlClientAdoNetClientData))]

// SqlDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader System.Data.SqlClient.SqlCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(SystemDataSqlClientAdoNetClientData))]

// DbDataReader Microsoft.Data.SqlClient.SqlCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

// Task<object> System.Data.SqlClient.SqlCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteScalarAsync(typeof(SystemDataSqlClientAdoNetClientData))]

// Task<object> Microsoft.Data.SqlClient.SqlCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(MicrosoftDataAdoNetClientData))]

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object System.Data.SqlClient.SqlCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(SystemDataAdoNetClientData))]
[assembly: CommandExecuteScalar(typeof(SystemDataSqlClientAdoNetClientData))]

// object Microsoft.Data.SqlClient.SqlCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(MicrosoftDataAdoNetClientData))]
