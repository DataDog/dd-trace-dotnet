// <copyright file="IgnoreActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Util;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Ignore Activity Handler catches existing integrations that also emits activities.
    /// </summary>
    internal sealed class IgnoreActivityHandler : IActivityHandler
    {
        private static readonly string[] SourcesNames =
        {
            "Couchbase.DotnetSdk.RequestTracer",
            "HttpHandlerDiagnosticListener",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "MySqlConnector",
            "Npgsql",
            "System.Net.Http.Desktop",
            "SqlClientDiagnosticListener",
            "Experimental.System.Net.NameResolution",
            "Experimental.System.Net.Http.Connections",
            "Experimental.System.Net.Sockets",
        };

        public static bool ShouldIgnoreByOperationName(string? operationName)
        {
            // We only have two ignored operation names for now, if we get more, we can be more
            // generalized, but this is called twice in hot path creation
            return operationName is not null
                && (operationName.StartsWith("System.Net.Http.", StringComparison.Ordinal)
                 || operationName.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal));
        }

        public static void IgnoreActivity<T>(T activity, Span? span)
            where T : IActivity
        {
            if (span is not null && activity is IW3CActivity w3cActivity)
            {
#pragma warning disable DDDUCK001 // Checking IDuckType for null
                if ((activity.Parent is null || activity.Parent.StartTimeUtc < span.StartTime.UtcDateTime)
                 && w3cActivity.SpanId is not null
                 && w3cActivity.TraceId is not null)
                {
                    // If we ignore the activity and there's an existing active span
                    // We modify the activity spanId with the one in the span
                    // The reason for that is in case this ignored activity is used
                    // for propagation then the current active span will appear as parentId
                    // in the context propagation, and we will keep the entire trace.

                    // TraceId (always 32 chars long even when using 64-bit ids)
                    w3cActivity.TraceId = span.Context.RawTraceId;

                    // SpanId (always 16 chars long)
                    w3cActivity.ParentSpanId = span.Context.RawSpanId;

                    // We clear internals Id and ParentId values to force recalculation.
                    w3cActivity.RawId = null;
                    w3cActivity.RawParentId = null;
                }
#pragma warning restore DDDUCK001 // Checking IDuckType for null
            }
        }

        public bool ShouldListenTo(string sourceName, string? version)
        {
            foreach (var ignoreSourceName in SourcesNames)
            {
                if (sourceName == ignoreSourceName)
                {
                    return true;
                }
            }

            return false;
        }

        public void ActivityStarted<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Propagate Trace and Parent Span ids
            IgnoreActivity(activity, (Span?)Tracer.Instance.ActiveScope?.Span);
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Do nothing
        }
    }
}
