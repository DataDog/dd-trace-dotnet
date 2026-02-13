// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.DatabaseMonitoring
{
    internal static class DatabaseMonitoringPropagator
    {
        private const string SqlCommentSpanService = "dddbs";
        private const string SqlCommentRootService = "ddps";
        private const string SqlCommentPeerService = "ddprs";
        private const string SqlCommentDbName = "dddb";
        private const string SqlCommentOuthost = "ddh";
        private const string SqlCommentVersion = "ddpv";
        private const string SqlCommentEnv = "dde";
        internal const string DbmPrefix = $"/*{SqlCommentSpanService}='";
        private const string ContextInfoParameterName = "@dd_trace_context";
        internal const string SetContextCommand = $"set context_info {ContextInfoParameterName}";

        private static readonly char[] PgHintPrefix = ['/', '*', '+']; // the characters that identify a pg_hint_plan
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatabaseMonitoringPropagator));

        // TODO: improve this rate limiting
        // to prevent too many similar errors in the logs. We assume that after 100 logs, the incremental value of more logs is negligible.
        private static int _remainingErrorLogs = 100;
        private static int _remainingDirectionErrorLogs = 100;
        private static int _remainingQuoteErrorLogs = 100;

        internal static bool PropagateDataViaComment(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbCommand command, string configuredServiceName, string? dbName, string? outhost, Span span, bool injectStoredProcedure)
        {
            Log.Debug("DBM: PropagateDataViaComment called. Integration: '{IntegrationId}', PropagationLevel: '{PropagationLevel}', CommandType: '{CommandType}'", integrationId, propagationLevel, command.CommandType);

            if (integrationId is not (IntegrationId.MySql or IntegrationId.Npgsql or IntegrationId.SqlClient or IntegrationId.Oracle) ||
                propagationLevel is not (DbmPropagationLevel.Service or DbmPropagationLevel.Full))
            {
                Log.Debug("DBM: PropagateDataViaComment skipped - unsupported integration or propagation level disabled. Integration: '{IntegrationId}', PropagationLevel: '{PropagationLevel}'", integrationId, propagationLevel);
                return false;
            }

            if (command.CommandType == CommandType.StoredProcedure && (!injectStoredProcedure || integrationId != IntegrationId.SqlClient))
            {
                Log.Debug("DBM: PropagateDataViaComment skipped for StoredProcedure. InjectStoredProcedure: {InjectStoredProcedure}, Integration: '{IntegrationId}'", injectStoredProcedure, integrationId);
                // We don't inject into StoredProcedures unless enabled as we change the commands
                // We don't inject into StoredProcedures unless we are in SqlClient
                return false;
            }

            var propagatorStringBuilder = StringBuilderCache.Acquire();
            var dddbs = span.Context.ServiceName;
            propagatorStringBuilder.Append(DbmPrefix).Append(Uri.EscapeDataString(dddbs)).Append('\'');

            string? ddprs = null;
            if (span.Tags is SqlV1Tags sqlTags)
            {
                if (sqlTags.PeerServiceSource == "peer.service")
                {
                    ddprs = sqlTags.PeerService;
                }
            }
            else
            {
                if (span.Tags.GetTag(Tags.PeerServiceRemappedFrom) != null)
                {
                    ddprs = span.Tags.GetTag(Tags.PeerService);
                }
            }

            if (ddprs != null)
            {
                propagatorStringBuilder.Append(',').Append(SqlCommentPeerService).Append("='").Append(Uri.EscapeDataString(ddprs)).Append('\'');
                Log.Information("DBM: Injecting ddprs (peer service) into SQL comment. Value: '{PeerService}'", ddprs);
            }
            else
            {
                Log.Information("DBM: NOT injecting ddprs. PeerServiceSource: '{Source}'", (span.Tags is SqlV1Tags st) ? st.PeerServiceSource : "N/A");
            }

            if (span.Context.TraceContext?.Environment is { } envTag)
            {
                propagatorStringBuilder.Append(',').Append(SqlCommentEnv).Append("='").Append(Uri.EscapeDataString(envTag)).Append('\'');
            }

            propagatorStringBuilder.Append(',').Append(SqlCommentRootService).Append("='").Append(Uri.EscapeDataString(configuredServiceName)).Append('\'');

            Log.Information(
                "DBM: SQL comment fields - dddbs (span service): '{SpanService}', ddps (root service): '{RootService}', dddb: '{DbName}', ddh: '{OutHost}'",
                new object[] { dddbs, configuredServiceName, dbName ?? "null", outhost ?? "null" });

            if (!string.IsNullOrEmpty(dbName))
            {
                propagatorStringBuilder.Append(value: ',').Append(SqlCommentDbName).Append("='").Append(Uri.EscapeDataString(dbName)).Append(value: '\'');
            }

            if (!string.IsNullOrEmpty(outhost))
            {
                propagatorStringBuilder.Append(value: ',').Append(SqlCommentOuthost).Append("='").Append(Uri.EscapeDataString(outhost)).Append(value: '\'');
            }

            if (span.Context.TraceContext?.ServiceVersion is { } versionTag)
            {
                propagatorStringBuilder.Append(',').Append(SqlCommentVersion).Append("='").Append(Uri.EscapeDataString(versionTag)).Append('\'');
            }

            var traceParentInjected = false;
            // For SqlServer & Oracle we don't inject the traceparent to avoid affecting performance, since those DBs generate a new plan for any query changes
            if (propagationLevel == DbmPropagationLevel.Full
             && integrationId is not (IntegrationId.SqlClient or IntegrationId.Oracle))
            {
                traceParentInjected = true;
                propagatorStringBuilder.Append(',').Append(W3CTraceContextPropagator.TraceParentHeaderName).Append("='").Append(W3CTraceContextPropagator.CreateTraceParentHeader(span.Context)).Append('\'');
                Log.Debug("DBM: Injecting traceparent in SQL comment (Full mode). Integration: '{IntegrationId}', TraceId: {TraceId}, SpanId: {SpanId}", integrationId, span.Context.RawTraceId, span.Context.RawSpanId);
            }
            else if (propagationLevel == DbmPropagationLevel.Full && integrationId is (IntegrationId.SqlClient or IntegrationId.Oracle))
            {
                Log.Debug("DBM: Skipping traceparent injection in SQL comment for SqlClient/Oracle (Full mode) to avoid query plan cache invalidation. Will attempt context injection instead. Integration: '{IntegrationId}'", integrationId);
            }
            else
            {
                Log.Debug("DBM: Service-level propagation only (no traceparent in comment). PropagationLevel: '{PropagationLevel}', Integration: '{IntegrationId}'", propagationLevel, integrationId);
            }

            propagatorStringBuilder.Append("*/");

            // modify the command to add the comment
            var commandText = command.CommandText ?? string.Empty;
            var propagationComment = StringBuilderCache.GetStringAndRelease(propagatorStringBuilder);
            Log.Information("DBM: Generated SQL comment for injection: '{SqlComment}'", propagationComment);
            Log.Information("DBM: Original command text (first 100 chars): '{CommandText}'", commandText.Length > 100 ? commandText.Substring(0, 100) : commandText);

            if (command.CommandType == CommandType.StoredProcedure && integrationId == IntegrationId.SqlClient)
            {
                /*
                * For CommandType.StoredProcedure, we need to modify the command text to use an EXEC statement and swap it to a CommandType.Text.
                * Without doing this we _cannot_ inject the comment as it will be taken as part of the stored procedure name.
                *    in SqlCommand.cs we have: rpc.rpcName = this.CommandText; // just get the raw command text
                * Injecting the comment in the CommandText would result in getting an exception that the StoredProcedure name wasn't found (and would look like an empty string).
                *
                * We CAN NOT safely do this though if the Command has any parameters that are not Input (e.g. Return, InputOutput, Output).
                *
                * What this function does is:
                *   - Swap to EXEC statement (which is a text command executing a stored procedure).
                *   - Build a Input parameter list for the EXEC statement, so that we can pass all the parameters to it.
                * Some helpful links:
                * reference RPC for stored procedure: https://github.com/microsoft/referencesource/blob/51cf7850defa8a17d815b4700b67116e3fa283c2/System.Data/System/Data/SqlClient/SqlCommand.cs#L5548
                * reference text SQL (with params): https://github.com/microsoft/referencesource/blob/51cf7850defa8a17d815b4700b67116e3fa283c2/System.Data/System/Data/SqlClient/SqlCommand.cs#L4488
                * reference text SQL (no params): https://github.com/microsoft/referencesource/blob/51cf7850defa8a17d815b4700b67116e3fa283c2/System.Data/System/Data/SqlClient/SqlCommand.cs#L4437
                */

                // check to see if we have any Return/InputOutput/Output parameters
                if (command.Parameters != null)
                {
                    foreach (DbParameter? param in command.Parameters)
                    {
                        if (param == null)
                        {
                            continue;
                        }

                        if (param.Direction != ParameterDirection.Input)
                        {
                            if (_remainingDirectionErrorLogs > 0)
                            {
                                var actualRemaining = Interlocked.Decrement(ref _remainingDirectionErrorLogs);
                                if (actualRemaining >= 0)
                                {
                                    Log.Warning<string, int>(
                                        "Cannot propagate DBM data for stored procedure with non-Input parameter '{ProcedureName}'. Only Input parameters are supported for DBM propagation (will log {N} more times and then stop).",
                                        commandText,
                                        actualRemaining);
                                }
                            }

                            return false;
                        }
                    }
                }

                var procName = command.CommandText ?? string.Empty;

                if (string.IsNullOrEmpty(procName))
                {
                    return false;
                }

                string? quotedName;
                try
                {
                    // dbo.SomeName -> [dbo].[SomeName]
                    // this uses some underlying SqlClient code that I've copied to parse the identifier
                    quotedName = VendoredSqlHelpers.ParseAndQuoteIdentifier(procName, isUdtTypeName: false);
                    if (string.IsNullOrEmpty(quotedName))
                    {
                        // if we can't parse the identifier, return false
                        if (_remainingQuoteErrorLogs > 0)
                        {
                            var actualRemaining = Interlocked.Decrement(ref _remainingQuoteErrorLogs);
                            if (actualRemaining >= 0)
                            {
                                Log.Error<string, int>(
                                    "Failed to parse/quote stored procedure '{ProcedureName}' for DBM propagation. (will log {N} more times and then stop).",
                                    commandText,
                                    actualRemaining);
                            }
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    if (_remainingQuoteErrorLogs > 0)
                    {
                        var actualRemaining = Interlocked.Decrement(ref _remainingQuoteErrorLogs);
                        if (actualRemaining >= 0)
                        {
                            Log.Error<string, int>(
                                ex,
                                "Exception while attempting to parse/quote stored procedure '{ProcedureName}' for DBM propagation. (will log {N} more times and then stop).",
                                commandText,
                                actualRemaining);
                        }
                    }

                    return false;
                }

                // Build parameter list for EXEC statement
                var paramList = new StringBuilder();
                if (command.Parameters != null)
                {
                    foreach (DbParameter? param in command.Parameters)
                    {
                        if (param == null)
                        {
                            continue;
                        }

                        // If we have any parameters that are not Input we can't do this safely as T-SQL vs RPC calls don't honor the parameters
                        if (param.Direction != ParameterDirection.Input)
                        {
                            // NOTE: we shouldn't hit this as we check above
                            paramList.Clear();
                            return false;
                        }

                        if (paramList.Length > 0)
                        {
                            // e.g. @Input=@Input, @Input2=@Input2
                            paramList.Append(", ");
                        }

                        paramList.Append(param.ParameterName).Append('=').Append(param.ParameterName);
                    }
                }

                // Change command type to Text, this allows us to use the EXEC statement
                // This changes how the SQL command is executed, but since we are only supporting INPUT parameters we should be fine
                command.CommandType = CommandType.Text;

                // Create EXEC statement with parameters
                // NOTE: EXECUTE is the exact same as EXEC, I chose EXEC arbitrarily
                if (paramList.Length > 0)
                {
                    command.CommandText = $"EXEC {quotedName} {paramList} {propagationComment}";
                }
                else
                {
                    command.CommandText = $"EXEC {quotedName} {propagationComment}";
                }

                // Log the command text for debugging purposes
                Log.Debug("Executing stored procedure with command text: {CommandText}", command.CommandText);
            }
            else if (ShouldAppend(integrationId, commandText))
            {
                Log.Information("DBM: Appending SQL comment (pg_hint_plan detected). Integration: '{IntegrationId}'", integrationId);
                command.CommandText = $"{commandText} {propagationComment}";
            }
            else
            {
                Log.Information("DBM: Prepending SQL comment. Integration: '{IntegrationId}'", integrationId);
                // prepending the propagation comment is the preferred way,
                // as this protects it from being truncated by the character limit if the command is very long.
                command.CommandText = $"{propagationComment} {commandText}";
            }

            Log.Information("DBM: Successfully injected SQL comment with DBM metadata. Integration: '{IntegrationId}', TraceParentInjected: {TraceParentInjected}", integrationId, traceParentInjected);
            Log.Information(
                "DBM: Final command text (first 200 chars): '{FinalCommandText}'",
                command.CommandText.Length > 200 ? command.CommandText.Substring(0, 200) : command.CommandText);

            return traceParentInjected;
        }

        internal static bool ShouldAppend(IntegrationId integrationId, string commandText)
        {
            // pg_hint_plan allows setting hints for the execution plan as the *first* comment. If such a hint is present,
            // we need to append our comment rather than prepend, to avoid invalidating the hint
            // see https://pg-hint-plan.readthedocs.io/en/latest/hint_details.html#syntax-and-placement
            return integrationId == IntegrationId.Npgsql && StartsWithHint(commandText);
        }

        /// <summary>
        /// Detect if a command contains a pg_hint_plan.
        /// </summary>
        private static bool StartsWithHint(string commandText)
        {
            // using .AsSpan() prevents from allocating a new string when we use TrimStart
            return commandText.AsSpan().TrimStart().StartsWith(PgHintPrefix);
        }

        /// <summary>
        /// Uses a sql instruction to set a context for the current connection, bearing the span ID and trace ID.
        /// This is meant to circumvent cache invalidation issues that occur when those values are injected in comment.
        /// Currently only working for Microsoft SQL Server (uses an instruction that is specific to it)
        /// </summary>
        /// <returns>True if the traceparent information was set</returns>
        internal static bool PropagateDataViaContext(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbCommand command, Span span)
        {
            Log.Debug("DBM: PropagateDataViaContext called. Integration: '{IntegrationId}', PropagationLevel: '{PropagationLevel}'", integrationId, propagationLevel);

            if (propagationLevel != DbmPropagationLevel.Full || integrationId != IntegrationId.SqlClient)
            {
                Log.Debug("DBM: PropagateDataViaContext skipped - only supported for SqlClient in Full mode. Integration: '{IntegrationId}', PropagationLevel: '{PropagationLevel}'", integrationId, propagationLevel);
                return false;
            }

            // NOTE: For Npgsql command.Connection throws NotSupportedException for NpgsqlDataSourceCommand (v7.0+)
            //       Since the feature isn't available for Npgsql we avoid this due to the integrationId check above
            if (command.Connection == null)
            {
                Log.Debug("DBM: PropagateDataViaContext skipped - command.Connection is null");
                return false;
            }

            if (command.Connection.State != ConnectionState.Open)
            {
                Log.Debug("PropagateDataViaContext did not have an Open connection, so it could not propagate Span data for DBM. Connection state was {ConnectionState}", command.Connection.State);

                return false;
            }

            Log.Debug("DBM: Starting context injection via SET CONTEXT_INFO for SQL Server. TraceId: {TraceId}, SpanId: {SpanId}", span.Context.RawTraceId, span.Context.RawSpanId);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            const byte version = 0; // version can have a maximum value of 15 in the current format
            var sampled = SamplingPriorityValues.IsKeep(span.Context.TraceContext.GetOrMakeSamplingDecision());
            var contextValue = BuildContextValue(version, sampled, span.SpanId, span.TraceId128);
            Log.Debug("DBM: Built CONTEXT_INFO binary value with trace/span information");

            using (var injectionCommand = command.Connection.CreateCommand())
            {
                // if there is a Transaction we need to copy it or our ExecuteNonQuery will throw
                injectionCommand.Transaction = command.Transaction;
                injectionCommand.CommandText = SetContextCommand;

                var parameter = injectionCommand.CreateParameter();
                parameter.ParameterName = ContextInfoParameterName;
                parameter.Value = contextValue;
                parameter.DbType = DbType.Binary;
                injectionCommand.Parameters.Add(parameter);

                try
                {
                    Log.Debug("DBM: Executing SET CONTEXT_INFO command on SQL Server connection");
                    injectionCommand.ExecuteNonQuery();
                    Log.Debug("DBM: SET CONTEXT_INFO executed successfully");
                }
                catch (Exception e)
                {
                    // stop logging the error after a while
                    if (_remainingErrorLogs > 0)
                    {
                        var actualRemaining = Interlocked.Decrement(ref _remainingErrorLogs);
                        if (actualRemaining >= 0)
                        {
                            Log.Error<string, int>(e, "Error setting context_info [{ContextValue}] for DB query, falling back to service only propagation mode. There won't be any link with APM traces. (will log this error {N} more time and then stop)", HexConverter.ToString(contextValue), actualRemaining);
                        }
                    }

                    return false;
                }
            }

            stopwatch.Stop();
            // Since sending the query to the DB can be a bit long, we register the time it took for transparency.
            // Not using _dd because we want the customers to be able to see that tag.
            span.SetMetric("dd.instrumentation.time_ms", stopwatch.Elapsed.TotalMilliseconds);

            Log.Information("DBM: Successfully injected traceparent via CONTEXT_INFO for SQL Server. Elapsed: {ElapsedMs}ms, TraceId: {TraceId}, SpanId: {SpanId}", stopwatch.Elapsed.TotalMilliseconds, span.Context.RawTraceId, span.Context.RawSpanId);
            return true;
        }

        /// <summary>
        /// Writes the given info in a byte array with the following format:
        /// 4 bits: protocol version, 3 bits: reserved, 1 bit: sampling decision, 64 bits: spanID, 128 bits: traceID
        /// </summary>
        private static byte[] BuildContextValue(byte version, bool isSampled, ulong spanId, TraceId traceId)
        {
            var sampled = isSampled ? 1 : 0;
            var versionAndSampling = (byte)(((version << 4) & 0b1111_0000) | (sampled & 0b0000_0001));
            var contextBytes = new byte[1 + sizeof(ulong) + TraceId.Size];

            var span = new Span<byte>(contextBytes) { [0] = versionAndSampling };
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1), spanId);
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1 + sizeof(ulong)), traceId.Upper);
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1 + sizeof(ulong) + sizeof(ulong)), traceId.Lower);

            return contextBytes;
        }
    }
}
