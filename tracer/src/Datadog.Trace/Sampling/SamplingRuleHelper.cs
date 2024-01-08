// <copyright file="SamplingRuleHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;

#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#else
using System.Text.RegularExpressions;
#endif

namespace Datadog.Trace.Sampling;

#nullable enable

internal class SamplingRuleHelper
{
    public static bool MatchSpanByTags(Span span, List<KeyValuePair<string, Regex>> tagRegexes)
    {
        foreach (var pair in tagRegexes)
        {
            var tagName = pair.Key;
            var tagRegex = pair.Value;
            var tagValue = GetSpanTag(span, tagName);

            if (tagValue is null || !tagRegex.Match(tagValue).Success)
            {
                // stop as soon as we find a tag that isn't set or doesn't match
                return false;
            }
        }

        // all specified tags exist and matched
        return true;
    }

    private static string? GetSpanTag(Span span, string tagName)
    {
        if (span.GetTag(tagName) is { } tagValue)
        {
            return tagValue;
        }

        // if the string tag doesn't exist, try to get it as a numeric tag...
        if (span.GetMetric(tagName) is not { } numericTagValue)
        {
            return null;
        }

        // ...but only if it is an integer
        var intValue = (int)numericTagValue;
        return Math.Abs(intValue - numericTagValue) < 0.0001 ? intValue.ToString(CultureInfo.InvariantCulture) : null;
    }
}
