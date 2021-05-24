// <copyright file="MySqlDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.MySql.MySqlConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

// Task<int> MySqlConnector.MySqlCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int MySql.Data.MySqlClient.MySqlCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(MySqlDataClientData))]
[assembly: CommandExecuteNonQuery(typeof(MySqlData8ClientData))]

// int MySqlConnector.MySqlCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CancellationToken)
 ********************************************************************************/

// Task<MySqlDataReader> MySqlConnector.MySqlCommand.ExecuteReaderAsync(CancellationToken)
[assembly: CommandExecuteReaderAsync(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<MySqlDataReader> MySqlConnector.MySqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteReaderWithBehaviorAsync(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> MySqlConnector.MySqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAsync(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * [*]DataReader .ExecuteReader()
 ********************************************************************************/

// MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(MySqlDataClientData))]
[assembly: CommandExecuteReader(typeof(MySqlData8ClientData))]

// MySqlDataReader MySqlConnector.MySqlCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteReader(CommandBehavior)
 ********************************************************************************/

// MySqlDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(MySqlDataClientData))]
[assembly: CommandExecuteReaderWithBehavior(typeof(MySqlData8ClientData))]

// MySqlDataReader MySqlConnector.MySqlCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader MySql.Data.MySqlClient.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(MySqlDataClientData))]
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(MySqlData8ClientData))]

// DbDataReader MySqlConnector.MySqlCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

// Task<object> MySqlConnector.MySqlCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(MySqlConnectorClientData))]

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object MySql.Data.MySqlClient.MySqlCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(MySqlDataClientData))]
[assembly: CommandExecuteScalar(typeof(MySqlData8ClientData))]

// object MySqlConnector.MySqlCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(MySqlConnectorClientData))]
