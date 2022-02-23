// <copyright file="IgnoreActivityHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
            "MassTransit",
            "Microsoft.AspNetCore",
            "Microsoft.EntityFrameworkCore",
            "MySqlConnector",
            "Npgsql",
            "System.Net.Http.Desktop",
            "SqlClientDiagnosticListener",
        };

        public bool ShouldListenTo(string sourceName, string version)
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
            if (activity.Parent is null && activity is IActivity5 activity5)
            {
                var span = Tracer.Instance.ActiveScope?.Span;
                if (span is not null)
                {
                    activity5.TraceId = span.TraceId.ToString("x32");
                    activity5.ParentSpanId = span.SpanId.ToString("x");
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
