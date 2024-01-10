// <copyright file="SamplingRulesFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Sampling;

internal static class SamplingRulesFormat
{
    public const string Unknown = "unknown";

    public const string Regex = "regex";

    public const string Glob = "glob";

    public static string Normalize(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            // default value if not specified
            return Regex;
        }

        return Regex.Equals(format, StringComparison.OrdinalIgnoreCase) ? Regex :
                Glob.Equals(format, StringComparison.OrdinalIgnoreCase) ? Glob :
                                                                          Unknown;
    }
}
