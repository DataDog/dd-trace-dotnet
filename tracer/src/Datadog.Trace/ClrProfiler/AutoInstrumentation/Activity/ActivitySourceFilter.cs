// <copyright file="ActivitySourceFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Activity.Handlers;
using Datadog.Trace.Sampling;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Activity
{
    /// <summary>
    /// Shared filter for Activity source names that should be ignored because they are already handled
    /// by dedicated Datadog integrations (e.g. ASP.NET Core, HttpClient, SqlClient) or because they
    /// have been explicitly disabled via <c>DD_TRACE_DISABLED_ACTIVITY_SOURCES</c>.
    /// Uses <see cref="IgnoreActivityHandler.SourcesNames"/> directly (rather than a second copy of the
    /// list) so the CallTarget interception path and the managed ActivityListener path cannot drift apart.
    /// </summary>
    internal static class ActivitySourceFilter
    {
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

            foreach (var ignored in IgnoreActivityHandler.SourcesNames)
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
