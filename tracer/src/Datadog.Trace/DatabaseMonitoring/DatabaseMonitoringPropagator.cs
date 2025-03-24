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
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers.Binary;
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

        private static int _remainingErrorLogs = 100; // to prevent too many similar errors in the logs. We assume that after 100 logs, the incremental value of more logs is negligible.

        internal static bool PropagateDataViaComment(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbCommand command, string configuredServiceName, string? dbName, string? outhost, Span span)
        {
            if (integrationId is not (IntegrationId.MySql or IntegrationId.Npgsql or IntegrationId.SqlClient or IntegrationId.Oracle) ||
                propagationLevel is not (DbmPropagationLevel.Service or DbmPropagationLevel.Full))
            {
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
            }

            if (span.Context.TraceContext?.Environment is { } envTag)
            {
                propagatorStringBuilder.Append(',').Append(SqlCommentEnv).Append("='").Append(Uri.EscapeDataString(envTag)).Append('\'');
            }

            propagatorStringBuilder.Append(',').Append(SqlCommentRootService).Append("='").Append(Uri.EscapeDataString(configuredServiceName)).Append('\'');

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
            }

            propagatorStringBuilder.Append("*/");

            // modify the command to add the comment
            var commandText = command.CommandText ?? string.Empty;
            var propagationComment = StringBuilderCache.GetStringAndRelease(propagatorStringBuilder);
            if (command.CommandType == CommandType.StoredProcedure)
            {
                // Save the original stored procedure name
                string procName = command.CommandText ?? string.Empty;

                if (string.IsNullOrEmpty(procName))
                {
                    return false;
                }

                // Build parameter list for EXEC statement
                StringBuilder paramList = new StringBuilder();
                foreach (DbParameter? param in command.Parameters)
                {
                    if (param == null)
                    {
                        continue;
                    }

                    // Skip return value parameters
                    if (param.Direction == ParameterDirection.ReturnValue)
                    {
                        continue;
                    }

                    if (paramList.Length > 0)
                    {
                        paramList.Append(", ");
                    }

                    paramList.Append(param.ParameterName).Append('=').Append(param.ParameterName);
                }

                // Change command type to Text
                command.CommandType = CommandType.Text;

                // Create EXEC statement with parameters
                if (paramList.Length > 0)
                {
                    command.CommandText = $"EXEC {procName} {paramList} {propagationComment}";
                }
                else
                {
                    command.CommandText = $"EXEC {procName} {propagationComment}";
                }

                Log.Debug("Executing stored procedure with command text: {CommandText}", command.CommandText);
            }
            else if (ShouldAppend(integrationId, commandText))
            {
                command.CommandText = $"{commandText} {propagationComment}";
            }
            else
            {
                // prepending the propagation comment is the preferred way,
                // as this protects it from being truncated by the character limit if the command is very long.
                command.CommandText = $"{propagationComment} {commandText}";
            }

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
        /// Currently only working for MSSQL (uses an instruction that is specific to it)
        /// </summary>
        /// <returns>True if the traceparent information was set</returns>
        internal static bool PropagateDataViaContext(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbCommand command, Span span)
        {
            if (propagationLevel != DbmPropagationLevel.Full || integrationId != IntegrationId.SqlClient)
            {
                return false;
            }

            // NOTE: For Npgsql command.Connection throws NotSupportedException for NpgsqlDataSourceCommand (v7.0+)
            //       Since the feature isn't available for Npgsql we avoid this due to the integrationId check above
            if (command.Connection == null)
            {
                return false;
            }

            if (command.Connection.State != ConnectionState.Open)
            {
                Log.Debug("PropagateDataViaContext did not have an Open connection, so it could not propagate Span data for DBM. Connection state was {ConnectionState}", command.Connection.State);

                return false;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            const byte version = 0; // version can have a maximum value of 15 in the current format
            var sampled = SamplingPriorityValues.IsKeep(span.Context.TraceContext.GetOrMakeSamplingDecision());
            var contextValue = BuildContextValue(version, sampled, span.SpanId, span.TraceId128);

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

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    // avoid building the string representation in the general case where debug is disabled
                    Log.Debug("Propagating span data for DBM for {Integration} via context_info with value {ContextValue} (propagation level: {PropagationLevel}", integrationId, HexConverter.ToString(contextValue), propagationLevel);
                }

                try
                {
                    injectionCommand.ExecuteNonQuery();
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

            // Since sending the query to the DB can be a bit long, we register the time it took for transparency.
            // Not using _dd because we want the customers to be able to see that tag.
            span.SetMetric("dd.instrumentation.time_ms", stopwatch.Elapsed.TotalMilliseconds);

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

            var span = new VendoredMicrosoftCode.System.Span<byte>(contextBytes) { [0] = versionAndSampling };
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1), spanId);
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1 + sizeof(ulong)), traceId.Upper);
            BinaryPrimitives.WriteUInt64BigEndian(span.Slice(1 + sizeof(ulong) + sizeof(ulong)), traceId.Lower);

            return contextBytes;
        }
    }
}
