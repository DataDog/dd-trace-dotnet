// <copyright file="DatabaseMonitoringPropagator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Numerics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

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
        internal static bool PropagateDataViaContext(Tracer tracer, DbmPropagationLevel propagationLevel, IntegrationId integrationId, IDbConnection? connection, string serviceName, Scope scope, SqlTags tags)
        {
            if (propagationLevel != DbmPropagationLevel.Full || integrationId != IntegrationId.SqlClient || connection == null)
            {
                return false;
            }

            // we want the instrumentation span to be a sibling of the actual query span
            var instrumentationParent = scope.Parent?.Span?.Context;
            // copy the tags so that modifications on one span don't impact the other
            var copyProcessor = new ITags.CopyProcessor<SqlTags>();
            tags.EnumerateTags(ref copyProcessor);
            using (var instrumentationScope = tracer.StartActiveInternal("set context_info", instrumentationParent, tags: copyProcessor.TagsCopy, serviceName: serviceName))
            {
                instrumentationScope.Span.Type = SpanTypes.Sql;
                // this tag serves as "documentation" for users to realize this is something done by the instrumentation
                instrumentationScope.Span.Tags.SetTag("dd.instrumentation", "true");

                byte version = 0; // version can have a maximum value of 15 in the current format
                var sampled = SamplingPriorityValues.IsKeep(scope.Span.Context.GetOrMakeSamplingDecision() ?? SamplingPriorityValues.Default);
                var contextValue = BuildContextValue(version, sampled, scope.Span.SpanId, scope.Span.TraceId128);
                var injectionSql = "set context_info @context";
                // important to set the resource name before running the command so that we don't re-instrument
                instrumentationScope.Span.ResourceName = injectionSql;

                using (var injectionCommand = connection.CreateCommand())
                {
                    injectionCommand.CommandText = injectionSql;

                    var parameter = injectionCommand.CreateParameter();
                    parameter.ParameterName = "@context";
                    parameter.Value = contextValue;
                    parameter.DbType = DbType.VarNumeric;
                    injectionCommand.Parameters.Add(parameter);

                    injectionCommand.ExecuteNonQuery();
                }
            } // closing instrumentation span

            // we don't want to measure the time spent in "set_context" in the actual query span
            scope.Span.ResetStartTime();
            return true;
        }

        /// <summary>
        /// Writes the given info in a biginteger with the following format:
        /// 4 bits: protocol version, 3 bits: reserved, 1 bit: sampling decision, 64 bits: spanID, 128 bits: traceID
        /// </summary>
        private static BigInteger BuildContextValue(byte version, bool isSampled, ulong spanId, TraceId traceId)
        {
            var sampled = isSampled ? 1 : 0;
            var versionAndSampling = (byte)(((version << 4) & 0b1111_0000) | (sampled & 0b0000_0001));
            var contextBytes = new byte[1 + sizeof(ulong) + TraceId.Size];
            // one pass to write 3 64 integers at once: span ID, upper, and lower traceID
            for (var i = 0; i < sizeof(ulong); i++)
            {
                var bitshift = i * sizeof(ulong); // we write the LSB first
                contextBytes[i] = (byte)(traceId.Lower >> bitshift);
                contextBytes[i + sizeof(ulong)] = (byte)(traceId.Upper >> bitshift);
                contextBytes[i + sizeof(ulong) + sizeof(ulong)] = (byte)(spanId >> bitshift);
            }

            contextBytes[contextBytes.Length - 1] = versionAndSampling;

            return new BigInteger(contextBytes); // little endian
        }
    }
}
