// <copyright file="ActivitySourceFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Shared filter for Activity source names that should be ignored because they are already handled
    /// by dedicated Datadog integrations (e.g. ASP.NET Core, HttpClient, SqlClient) or because they
    /// have been explicitly disabled via <c>DD_TRACE_DISABLED_ACTIVITY_SOURCES</c>.
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

        private static List<Regex>? _disabledSourceGlobs;
        private static bool _disableAll;

        /// <summary>
        /// Returns true if the Activity from the given source should be ignored by the CallTarget
        /// interception path (because it is handled by a separate Datadog integration or was
        /// explicitly disabled via configuration).
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

            // Check DD_TRACE_DISABLED_ACTIVITY_SOURCES glob patterns
            if (_disableAll)
            {
                return true;
            }

            _disabledSourceGlobs ??= PopulateDisabledGlobs();
            foreach (var regex in _disabledSourceGlobs)
            {
                if (regex.IsMatch(sourceName))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<Regex> PopulateDisabledGlobs()
        {
            var globs = new List<Regex>();
            var toDisable = Tracer.Instance.Settings.DisabledActivitySources;
            if (toDisable is null || toDisable.Length == 0)
            {
                return globs;
            }

            foreach (var disabledSourceNameGlob in toDisable)
            {
                var globRegex = RegexBuilder.Build(disabledSourceNameGlob, SamplingRulesFormat.Glob, RegexBuilder.DefaultTimeout);
                if (globRegex is null)
                {
                    _disableAll = true;
                    return [];
                }

                globs.Add(globRegex);
            }

            return globs;
        }
    }
}
