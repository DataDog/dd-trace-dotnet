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

    public static bool IsValid(string format, out string normalized)
    {
        if (format == null!)
        {
            // default value if not specified
            normalized = Regex;
            return true;
        }

        format = format.Trim();

        if (Regex.Equals(format, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Regex;
            return true;
        }

        if (Glob.Equals(format, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Glob;
            return true;
        }

        normalized = Unknown;
        return false;
    }
}
