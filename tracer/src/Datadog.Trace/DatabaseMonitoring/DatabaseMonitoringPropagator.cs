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

        internal static bool PropagateDataViaComment(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbCommand command, string configuredServiceName, string? dbName, string? outhost, Span span, bool injectStoredProcedure)
        {
            if (integrationId is not (IntegrationId.MySql or IntegrationId.Npgsql or IntegrationId.SqlClient or IntegrationId.Oracle) ||
                propagationLevel is not (DbmPropagationLevel.Service or DbmPropagationLevel.Full))
            {
                return false;
            }

            if (!injectStoredProcedure && command.CommandType == CommandType.StoredProcedure)
            {
                // we don't want to inject the comment for stored procedures if it has been disabled explicitly
                return false;
            }

            if (command.CommandType == CommandType.StoredProcedure && integrationId != IntegrationId.SqlClient)
            {
                // we only support stored procedures for SqlClient
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

            if (command.CommandType == CommandType.StoredProcedure && integrationId == IntegrationId.SqlClient)
            {
                /*
                * For CommandType.StoredProcedure, we need to modify the command text to use an EXEC statement and swap it to a CommandType.Text.
                * Without doing this we _cannot_ inject the comment as it will be taken as part of the stored procedure name.
                *    in SqlCommand.cs we have: rpc.rpcName = this.CommandText; // just get the raw command text
                * Injecting the comment in the CommandText would result in getting an exception that the StoredProcedure name wasn't found (and would look like an empty string).
                *
                * What this function does is:
                *   - Swap to EXEC statement (which is a text command executing a stored procedure).
                *   - Build a parameter list for the EXEC statement, so that we can pass all the parameters to it.
                *   - These are the Input and Output parameters, but they may not be present
                *   - We don't modify the parameters on the Command itself, this will still be handled fine by SQL Server from testing
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
                            return false;
                        }
                    }
                }

                // Save the original stored procedure name
                string procName = command.CommandText ?? string.Empty;

                if (string.IsNullOrEmpty(procName))
                {
                    return false;
                }

                string quotedName = string.Empty;

                try
                {
                    quotedName = ParseAndQuoteIdentifier(procName, isUdtTypeName: false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to parse/quote stored procedure, no DBM data will be propagated");
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

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    // Log the command text for debugging purposes
                    Log.Debug("Executing stored procedure with command text: {CommandText}", command.CommandText);
                }
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
        /// Currently only working for Microsoft SQL Server (uses an instruction that is specific to it)
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

        // parse an string of the form db.schema.name where any of the three components
        // might have "[" "]" and dots within it.
        // returns:
        //   [0] dbname (or null)
        //   [1] schema (or null)
        //   [2] name
        // NOTE: if perf/space implications of Regex is not a problem, we can get rid
        // of this and use a simple regex to do the parsing
        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/src/Microsoft/Data/SqlClient/SqlParameter.cs#L2433
        internal static string[] ParseTypeName(string typeName, bool isUdtTypeName)
        {
            try
            {
                string errorMsg = string.Empty;
                return MultipartIdentifier.ParseMultipartIdentifier(typeName, "[\"", "]\"", '.', 3, true, errorMsg, true);
            }
            catch (ArgumentException)
            {
                throw new Exception();
            }
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6496
        // Adds quotes to each part of a SQL identifier that may be multi-part, while leaving
        //  the result as a single composite name.
        private static string ParseAndQuoteIdentifier(string identifier, bool isUdtTypeName)
        {
            string[] strings = ParseTypeName(identifier, isUdtTypeName);
            return QuoteIdentifier(strings);
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/netcore/src/Microsoft/Data/SqlClient/SqlCommand.cs#L6502
        private static string QuoteIdentifier(ReadOnlySpan<string> strings)
        {
            StringBuilder bld = new StringBuilder();

            // Stitching back together is a little tricky. Assume we want to build a full multi-part name
            //  with all parts except trimming separators for leading empty names (null or empty strings,
            //  but not whitespace). Separators in the middle should be added, even if the name part is
            //  null/empty, to maintain proper location of the parts.
            for (int i = 0; i < strings.Length; i++)
            {
                if (0 < bld.Length)
                {
                    bld.Append('.');
                }

                if (strings[i] != null && 0 != strings[i].Length)
                {
                    AppendQuotedString(bld, "[", "]", strings[i]);
                }
            }

            return bld.ToString();
        }

        // https://github.com/dotnet/SqlClient/blob/414f016540932d339054c61abc5ae838401cdb06/src/Microsoft.Data.SqlClient/src/Microsoft/Data/Common/AdapterUtil.cs#L547
        internal static string AppendQuotedString(StringBuilder buffer, string quotePrefix, string quoteSuffix, string unQuotedString)
        {
            if (!string.IsNullOrEmpty(quotePrefix))
            {
                buffer.Append(quotePrefix);
            }

            // Assuming that the suffix is escaped by doubling it. i.e. foo"bar becomes "foo""bar".
            if (!string.IsNullOrEmpty(quoteSuffix))
            {
                int start = buffer.Length;
                buffer.Append(unQuotedString);
                buffer.Replace(quoteSuffix, quoteSuffix + quoteSuffix, start, unQuotedString.Length);
                buffer.Append(quoteSuffix);
            }
            else
            {
                buffer.Append(unQuotedString);
            }

            return buffer.ToString();
        }
    }
}
