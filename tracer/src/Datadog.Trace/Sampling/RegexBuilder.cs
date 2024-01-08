// <copyright file="RegexBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#else
using System.Text.RegularExpressions;
#endif

#nullable enable

namespace Datadog.Trace.Sampling;

internal static class RegexBuilder
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(RegexBuilder));

    public static Regex? Build(string? pattern, string format)
    {
        if (pattern is null)
        {
            return null;
        }

#if NETCOREAPP3_1_OR_GREATER
        var options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;
#else
        var options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
#endif

        var timeout = TimeSpan.FromSeconds(1);

        switch (format)
        {
            case SamplingRulesFormat.Regex:
                return new Regex(
                    WrapWithLineCharacters(pattern),
                    options,
                    timeout);

            case SamplingRulesFormat.Glob:
                // convert glob pattern to regex
                return new Regex(
                    $"^{Regex.Escape(pattern).Replace("\\?", ".").Replace("\\*", ".*")}$",
                    options,
                    timeout);

            default:
                // ReSharper disable once RedundantNameQualifier ("Util." is only redundant for some target frameworks)
                Util.ThrowHelper.ThrowArgumentOutOfRangeException(
                    nameof(format),
                    format,
                    "Invalid match pattern format. Valid values are 'regex' or 'glob'.");
                return null; // unreachable
        }
    }

    private static string WrapWithLineCharacters(string regex)
    {
        var hasLineStart = regex.StartsWith("^");
        var hasLineEnd = regex.EndsWith("$");

        return hasLineStart
                 ? (hasLineEnd ? regex : $"{regex}$")
                 : (hasLineEnd ? $"^{regex}" : $"^{regex}$");
    }
}
