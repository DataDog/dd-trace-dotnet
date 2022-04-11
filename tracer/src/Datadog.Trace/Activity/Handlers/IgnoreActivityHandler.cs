// <copyright file="IgnoreActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Activity.DuckTypes;

namespace Datadog.Trace.Activity.Handlers
{
    /// <summary>
    /// Ignore Activity Handler catches existing integrations that also emits activities.
    /// </summary>
    internal class IgnoreActivityHandler : IActivityHandler
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
        };

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
            var activeSpan = (Span?)Tracer.Instance.ActiveScope?.Span;
            if (activeSpan is not null && activity is IW3CActivity w3cActivity)
            {
                if (activity.Parent is null || activity.Parent.StartTimeUtc < activeSpan.StartTime.UtcDateTime)
                {
                    // If we ignore the activity and there's an existing active span
                    // We modify the activity spanId with the one in the span
                    // The reason for that is in case this ignored activity is used
                    // for propagation then the current active span will appear as parentId
                    // in the context propagation, and we will keep the entire trace.

                    // TraceId
                    w3cActivity.TraceId = string.IsNullOrWhiteSpace(activeSpan.Context.RawTraceId) ?
                                              activeSpan.TraceId.ToString("x32") : activeSpan.Context.RawTraceId;

                    // SpanId
                    w3cActivity.ParentSpanId = string.IsNullOrWhiteSpace(activeSpan.Context.RawSpanId) ?
                                                   activeSpan.SpanId.ToString("x16") : activeSpan.Context.RawSpanId;

                    // We clear internals Id and ParentId values to force recalculation.
                    w3cActivity.RawId = null;
                    w3cActivity.RawParentId = null;
                }
            }
        }

        public void ActivityStopped<T>(string sourceName, T activity)
            where T : IActivity
        {
            // Do nothing
        }
    }
}
