// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
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
        private const string SqlCommentDbName = "dddb";
        private const string SqlCommentOuthost = "ddh";
        private const string SqlCommentVersion = "ddpv";
        private const string SqlCommentEnv = "dde";
        internal const string DbmPrefix = $"/*{SqlCommentSpanService}='";
        private const string ContextInfoParameterName = "@dd_trace_context";
        internal const string SetContextCommand = $"set context_info {ContextInfoParameterName}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatabaseMonitoringPropagator));

        internal static string PropagateDataViaComment(DbmPropagationLevel propagationStyle, string configuredServiceName, string? dbName, string? outhost, Span span, IntegrationId integrationId, out bool traceParentInjected)
        {
            traceParentInjected = false;

            if (integrationId is IntegrationId.MySql or IntegrationId.Npgsql or IntegrationId.SqlClient or IntegrationId.Oracle &&
                (propagationStyle is DbmPropagationLevel.Service or DbmPropagationLevel.Full))
            {
                var propagatorStringBuilder = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                var dddbs = (span.Tags is SqlV1Tags sqlTags) ? sqlTags.PeerService : span.Context.ServiceNameInternal;
                propagatorStringBuilder.Append(DbmPrefix).Append(Uri.EscapeDataString(dddbs)).Append('\'');

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

                // For SqlServer & Oracle we don't inject the traceparent to avoid affecting performance, since those DBs generate a new plan for any query changes
                if (propagationStyle == DbmPropagationLevel.Full
                 && integrationId is not (IntegrationId.SqlClient or IntegrationId.Oracle))
                {
                    traceParentInjected = true;
                    propagatorStringBuilder.Append(',').Append(W3CTraceContextPropagator.TraceParentHeaderName).Append("='").Append(W3CTraceContextPropagator.CreateTraceParentHeader(span.Context)).Append("'*/");
                }
                else
                {
                    propagatorStringBuilder.Append("*/");
                }

                return StringBuilderCache.GetStringAndRelease(propagatorStringBuilder);
            }

            return string.Empty;
        }

        /// <summary>
        /// Uses a sql instruction to set a context for the current connection, bearing the span ID and trace ID.
        /// This is meant to circumvent cache invalidation issues that occur when those values are injected in comment.
        /// Currently only working for MSSQL (uses an instruction that is specific to it)
        /// </summary>
        /// <returns>True if the traceparent information was set</returns>
        internal static bool PropagateDataViaContext(DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbConnection? connection, Span span)
        {
            if (propagationLevel != DbmPropagationLevel.Full || integrationId != IntegrationId.SqlClient || connection == null)
            {
                return false;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            const byte version = 0; // version can have a maximum value of 15 in the current format
            var sampled = SamplingPriorityValues.IsKeep(span.Context.TraceContext.GetOrMakeSamplingDecision());
            var contextValue = BuildContextValue(version, sampled, span.SpanId, span.TraceId128);

            using (var injectionCommand = connection.CreateCommand())
            {
                injectionCommand.CommandText = SetContextCommand;

                var parameter = injectionCommand.CreateParameter();
                parameter.ParameterName = ContextInfoParameterName;
                parameter.Value = contextValue;
                parameter.DbType = DbType.Binary;
                injectionCommand.Parameters.Add(parameter);

                injectionCommand.ExecuteNonQuery();

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    // avoid building the string representation in the general case where debug is disabled
                    Log.Debug("Span data for DBM propagated for {Integration} via context_info with value {ContextValue} (propagation level: {PropagationLevel}", integrationId, HexConverter.ToString(contextValue), propagationLevel);
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
