using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.Npgsql.NpgsqlConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

// Task<int> Npgsql.NpgsqlCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(NpgsqlClientData))]

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int Npgsql.NpgsqlCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(NpgsqlClientData))]

/********************************************************************************
 * Task<[*]DataReader> .ExecuteReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<NpgsqlDataReader> Npgsql.NpgsqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteReaderWithBehaviorAsync(typeof(NpgsqlClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> Npgsql.NpgsqlCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAsync(typeof(NpgsqlClientData))]

/********************************************************************************
 * [*]DataReader .ExecuteReader()
 ********************************************************************************/

// NpgsqlDataReader Npgsql.NpgsqlCommand.ExecuteReader()
[assembly: CommandExecuteReader(typeof(NpgsqlClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteReader(CommandBehavior)
 ********************************************************************************/

// NpgsqlDataReader Npgsql.NpgsqlCommand.ExecuteReader(CommandBehavior)
[assembly: CommandExecuteReaderWithBehavior(typeof(NpgsqlClientData))]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader Npgsql.NpgsqlCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(NpgsqlClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

// Task<object> Npgsql.NpgsqlCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(NpgsqlClientData))]

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object Npgsql.NpgsqlCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(NpgsqlClientData))]
