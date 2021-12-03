// <copyright file="AdoNetDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

// Task<int> System.Data.Common.DbCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataCommonClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> System.Data.Common.DbCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataCommonClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

// Task<object> System.Data.Common.DbCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteScalarAsync(typeof(SystemDataCommonClientData))]

/********************************************************************************
 * int .ExecuteNonQuery()
 ********************************************************************************/

// int System.Data.Common.DbCommand.ExecuteNonQuery()
[assembly: CommandExecuteNonQuery(typeof(SystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteNonQuery(typeof(NetStandardSystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteNonQuery(typeof(SystemDataCommonClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]

/********************************************************************************
 * object .ExecuteScalar()
 ********************************************************************************/

// object System.Data.Common.DbCommand.ExecuteScalar()
[assembly: CommandExecuteScalar(typeof(SystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteScalar(typeof(NetStandardSystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteScalar(typeof(SystemDataCommonClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]

/********************************************************************************
 * [*]DataReader [Command].ExecuteDbDataReader(CommandBehavior)
 ********************************************************************************/

// DbDataReader System.Data.Common.DbCommand.ExecuteDbDataReader(CommandBehavior)
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(SystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(NetStandardSystemDataForAbstractClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
[assembly: CommandExecuteDbDataReaderWithBehavior(typeof(SystemDataCommonClientData), Datadog.Trace.ClrProfiler.IntegrationType.Derived)]
