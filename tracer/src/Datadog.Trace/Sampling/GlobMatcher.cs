// <copyright file="GlobMatcher.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#else
using System.Text.RegularExpressions;
#endif

#nullable enable

namespace Datadog.Trace.Sampling;

internal static class GlobMatcher
{
    public static Regex BuildRegex(string glob, RegexOptions? regexOptions = null, TimeSpan? regexTimeout = null)
    {
        // TODO default glob (maybe null/empty/whitespace) should be *
        var regexPattern = $"^{Regex.Escape(glob).Replace("\\?", ".").Replace("\\*", ".*")}$";
        var timeout = regexTimeout ?? TimeSpan.FromSeconds(1);

#if NETCOREAPP3_1_OR_GREATER
        var options = regexOptions ?? RegexOptions.Compiled | RegexOptions.NonBacktracking;
#else
        var options = regexOptions ?? RegexOptions.Compiled;
#endif

        return new Regex(regexPattern, options, timeout);
    }
}
