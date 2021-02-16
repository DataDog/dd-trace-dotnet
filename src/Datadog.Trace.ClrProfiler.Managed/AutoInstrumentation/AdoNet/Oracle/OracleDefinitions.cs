using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Oracle.OracleConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(OracleClientData))]
[assembly: CommandExecuteNonQuery(typeof(OracleCoreClientData))]

// int Oracle.DataAccess.Client.OracleCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(OracleDataAccessClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * [*]DataReader .ExecuteReader()
 ********************************************************************************/

// OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(OracleClientData))]
[assembly: CommandExecuteReader(typeof(OracleCoreClientData))]

// OracleDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(OracleDataAccessClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteReader(CommandBehavior)
 ********************************************************************************/

// OracleDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(OracleClientData))]
[assembly: CommandExecuteReaderWithBehavior(typeof(OracleCoreClientData))]

// OracleDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(OracleDataAccessClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(OracleClientData))]
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(OracleCoreClientData))]

// DbDataReader Oracle.DataAccess.Client.OracleCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(OracleDataAccessClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object Oracle.ManagedDataAccess.Client.OracleCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(OracleClientData))]
[assembly: CommandExecuteScalar(typeof(OracleCoreClientData))]

// object Oracle.DataAccess.Client.OracleCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(OracleDataAccessClientData))]
