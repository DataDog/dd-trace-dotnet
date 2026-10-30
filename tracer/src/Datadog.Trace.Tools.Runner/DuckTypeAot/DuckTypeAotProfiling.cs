// <copyright file="DuckTypeAotProfiling.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner.DuckTypeAot
{
    /// <summary>
    /// The timings of the registry generator's phases, written to the console when DD_TRACE_DUCKTYPE_AOT_PROFILE is set.
    /// </summary>
    internal static class DuckTypeAotProfiling
    {
        private const string EnvironmentVariable = "DD_TRACE_DUCKTYPE_AOT_PROFILE";

        internal static bool IsEnabled()
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariable);
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        internal static void WriteMetric(string message)
        {
            AnsiConsole.MarkupLine($"[blue]ducktype-aot profile:[/] {message.EscapeMarkup()}");
        }
    }
}
