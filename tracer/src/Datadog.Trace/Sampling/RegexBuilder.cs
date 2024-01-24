// <copyright file="RegexBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#else
using System.Text.RegularExpressions;
#endif

#nullable enable

namespace Datadog.Trace.Sampling;

internal static class RegexBuilder
{
    public static Regex? Build(string? pattern, string format)
    {
        if (pattern is null)
        {
            return null;
        }

#if NETCOREAPP3_1_OR_GREATER
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking;
#else
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase;
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
                return null; // should be unreachable because we validate the format earlier
        }
    }

    public static List<KeyValuePair<string, Regex?>> Build(ICollection<KeyValuePair<string, string?>> patterns, string format)
    {
        if (patterns is { Count: > 0 })
        {
            var regexList = new List<KeyValuePair<string, Regex?>>(patterns.Count);

            foreach (var pattern in patterns)
            {
                var regex = Build(pattern.Value, format);

                if (regex != null)
                {
                    regexList.Add(new KeyValuePair<string, Regex?>(pattern.Key, regex));
                }
            }

            return regexList;
        }

        return [];
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
