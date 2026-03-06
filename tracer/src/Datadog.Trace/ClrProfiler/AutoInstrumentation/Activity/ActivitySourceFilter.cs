// <copyright file="ActivitySourceFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Shared filter for Activity source names that should be ignored because they are already handled
    /// by dedicated Datadog integrations (e.g. ASP.NET Core, HttpClient, SqlClient).
    /// This mirrors the <c>IgnoreActivityHandler.SourcesNames</c> list used by the managed ActivityListener.
    /// </summary>
    internal static class ActivitySourceFilter
    {
        private static readonly string[] IgnoredSources =
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

        /// <summary>
        /// Returns true if the Activity from the given source should be ignored by the CallTarget
        /// interception path (because it is handled by a separate Datadog integration).
        /// </summary>
        public static bool ShouldIgnore(string sourceName, string? version)
        {
            if (string.IsNullOrEmpty(sourceName))
            {
                return false;
            }

            foreach (var ignored in IgnoredSources)
            {
                if (string.Equals(sourceName, ignored, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
