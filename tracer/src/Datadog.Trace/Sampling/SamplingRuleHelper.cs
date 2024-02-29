// <copyright file="SamplingRuleHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling;

#nullable enable

internal static class SamplingRuleHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SamplingRuleHelper));

    public static bool IsMatch(
        Span span,
        Regex? serviceNameRegex,
        Regex? operationNameRegex,
        Regex? resourceNameRegex,
        List<KeyValuePair<string, Regex?>>? tagRegexes,
        out bool timedOut)
    {
        timedOut = false;

        if (span == null!)
        {
            return false;
        }

        try
        {
            // if a regex is null (not specified), it always matches.
            // stop as soon as we find a non-match.
            return IsMatch(serviceNameRegex, span.ServiceName) &&
                   IsMatch(operationNameRegex, span.OperationName) &&
                   IsMatch(resourceNameRegex, span.ResourceName) &&
                   MatchSpanByTags(span, tagRegexes);
        }
        catch (RegexMatchTimeoutException e)
        {
            // flag rule so we don't try to use one of its regexes again
            timedOut = true;

            Log.Error(
                e,
                """Regex timed out when trying to match value "{Input}" against pattern "{Pattern}".""",
                e.Input,
                e.Pattern);

            return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsMatch(Regex? regex, string? input)
    {
        if (regex is null)
        {
            // if a regex is null (not specified), it always matches.
            return true;
        }

        if (input is null)
        {
            return false;
        }

        return regex.Match(input).Success;
    }

    private static bool MatchSpanByTags(Span span, List<KeyValuePair<string, Regex?>>? tagRegexes)
    {
        if (tagRegexes is null || tagRegexes.Count == 0)
        {
            // if a regex is null (not specified), it always matches.
            return true;
        }

        foreach (var pair in tagRegexes)
        {
            var tagName = pair.Key;
            var tagRegex = pair.Value;

            if (tagRegex is null)
            {
                // if a regex is null (not specified), it always matches.
                continue;
            }

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
