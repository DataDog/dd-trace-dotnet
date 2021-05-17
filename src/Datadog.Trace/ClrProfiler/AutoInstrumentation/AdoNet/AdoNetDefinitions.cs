// <copyright file="AdoNetDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodAttribute;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetConstants;

/********************************************************************************
 * Task<int> .ExecuteNonQueryAsync(CancellationToken)
 ********************************************************************************/

// Task<int> System.Data.Common.DBCommand.ExecuteNonQueryAsync(CancellationToken)
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteNonQueryAsync(typeof(SystemDataCommonClientData))]

/********************************************************************************
 * Task<DbDataReader> .ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
 ********************************************************************************/

// Task<DbDataReader> System.Data.Common.DBCommand.ExecuteDbDataReaderAsync(CommandBehavior, CancellationToken)
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteDbDataReaderWithBehaviorAndCancellationAsync(typeof(SystemDataCommonClientData))]

/********************************************************************************
 * Task<object> .ExecuteScalarAsync(CancellationToken)
 ********************************************************************************/

// Task<object> System.Data.Common.DBCommand.ExecuteScalarAsync(CancellationToken)
[assembly: CommandExecuteScalarAsync(typeof(SystemDataClientData))]
[assembly: CommandExecuteScalarAsync(typeof(SystemDataCommonClientData))]
