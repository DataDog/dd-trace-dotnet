// <copyright file="StripInjectedContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

/// <summary>
/// Removes "_datadog":{...} trace-context carriers injected by Datadog into Lambda event payloads
/// (EventBridge detail, SNS Message, SQS body) before the customer handler reads them.
///
/// Carriers are removed by cutting text ranges from the JSON rather than parse-and-re-serialize,
/// so customer data (numbers, key order, formatting) is preserved.
/// Carriers inside string-encoded JSON (SNS Message, SQS body) are decoded,
/// stripped recursively up to <see cref="MaxDepth"/> levels, and re-encoded.
/// A "_datadog" key whose value is not an object is treated as customer data and kept.
/// Fail-open: on any error the original stream is kept.
/// </summary>
internal static class StripInjectedContext
{
    /// <summary>SNS-to-SQS fan-out needs 2 levels of string-encoded JSON; one more is a safety margin.</summary>
    internal const int MaxDepth = 3;

    private const string CarrierKey = "_datadog";
    private const string QuotedCarrierKey = "\"" + CarrierKey + "\"";

    private enum CarrierMatch
    {
        /// <summary>The token is not a "_datadog" key with an object value.</summary>
        None,

        /// <summary>A carrier was found; the out parameters hold the range to cut.</summary>
        Found,

        /// <summary>The carrier object never closes (malformed JSON).</summary>
        Unclosed,
    }

    /// <summary>
    /// If <paramref name="enabled"/> is true and <paramref name="json"/> contains a "_datadog" carrier,
    /// returns a new MemoryStream over the stripped payload.
    /// Returns null when disabled, when nothing was removed, or on any error (fail-open),
    /// so the caller keeps the original stream.
    /// </summary>
    internal static MemoryStream? StripStream(string json, bool enabled)
    {
        if (!enabled || string.IsNullOrEmpty(json) || json.IndexOf(CarrierKey, StringComparison.Ordinal) < 0)
        {
            return null;
        }

        // Invalid UTF-8 was decoded to U+FFFD and would be re-encoded as such, so keep the payload as is.
        if (json.IndexOf('\uFFFD') >= 0)
        {
            return null;
        }

        try
        {
            var stripped = StripInternal(json);
            if (ReferenceEquals(stripped, json))
            {
                return null;
            }

            return new MemoryStream(Encoding.UTF8.GetBytes(stripped));
        }
        catch (Exception ex)
        {
            LambdaCommon.Log("Unable to strip _datadog from Lambda payload; using original", ex, debug: false);
            return null;
        }
    }

    /// <summary>
    /// Strips "_datadog":{...} from <paramref name="json"/>. Unconditional (no config check).
    /// Internal so unit tests and benchmarks can call it directly.
    /// </summary>
    internal static string StripInternal(string json)
        => string.IsNullOrEmpty(json) ? json : StripJson(json, depth: 0);

    /// <summary>
    /// Walks string tokens once. A "_datadog" key with an object value is cut out;
    /// any other string that contains the carrier key is treated as encoded JSON and
    /// stripped recursively. Returns <paramref name="json"/> unchanged when nothing matched.
    /// </summary>
    private static string StripJson(string json, int depth)
    {
        StringBuilder? builder = null;
        int copyFrom = 0;
        int nextKey = -1;
        int start = json.IndexOf('"');

        while (start >= 0)
        {
            // nextKey is the next "_datadog" occurrence at or after start; a string token can only
            // contain it when nextKey < end, so tokens without it are skipped without decoding.
            if (nextKey < start)
            {
                nextKey = json.IndexOf(CarrierKey, start, StringComparison.Ordinal);
                if (nextKey < 0)
                {
                    break;
                }
            }

            int end = StringEnd(json, start);
            if (end < 0)
            {
                break;
            }

            var match = FindCarrier(json, start, end, copyFrom, out int removeStart, out int removeEnd);
            if (match == CarrierMatch.Unclosed)
            {
                // Malformed JSON: leave this level untouched
                if (builder != null)
                {
                    StringBuilderCache.Release(builder);
                }

                return json;
            }

            if (match == CarrierMatch.Found)
            {
                builder ??= StringBuilderCache.Acquire(json.Length);
                builder.Append(json, copyFrom, removeStart - copyFrom);
                copyFrom = removeEnd;
                end = removeEnd;
            }
            else if (nextKey < end && depth < MaxDepth && !IsKey(json, end))
            {
                var replacement = StripEncodedJson(json.Substring(start, end - start), depth);
                if (replacement != null)
                {
                    builder ??= StringBuilderCache.Acquire(json.Length);
                    builder.Append(json, copyFrom, start - copyFrom);
                    builder.Append(replacement);
                    copyFrom = end;
                }
            }

            start = json.IndexOf('"', end);
        }

        if (builder == null)
        {
            return json;
        }

        builder.Append(json, copyFrom, json.Length - copyFrom);
        return StringBuilderCache.GetStringAndRelease(builder);
    }

    // Finds a "_datadog":{...} entry starting at the key token [keyStart, keyEnd).
    // On Found, [removeStart, removeEnd) is the range to cut, including one adjacent comma.
    private static CarrierMatch FindCarrier(string json, int keyStart, int keyEnd, int copyFrom, out int removeStart, out int removeEnd)
    {
        removeStart = -1;
        removeEnd = -1;

        if (keyEnd - keyStart != QuotedCarrierKey.Length
            || string.CompareOrdinal(json, keyStart, QuotedCarrierKey, 0, QuotedCarrierKey.Length) != 0)
        {
            return CarrierMatch.None;
        }

        int colon = SkipWhitespace(json, keyEnd);
        if (colon >= json.Length || json[colon] != ':')
        {
            return CarrierMatch.None;
        }

        int valueStart = SkipWhitespace(json, colon + 1);
        if (valueStart >= json.Length || json[valueStart] != '{')
        {
            // Value is not an object: treat as customer data, keep it
            return CarrierMatch.None;
        }

        int valueEnd = ObjectEnd(json, valueStart);
        if (valueEnd < 0)
        {
            return CarrierMatch.Unclosed;
        }

        removeStart = keyStart;
        removeEnd = valueEnd;

        // Prefer to include a preceding comma (doesn't reuse one already cut); otherwise a trailing one.
        int before = SkipWhitespaceBackward(json, keyStart - 1);
        if (before >= copyFrom && json[before] == ',')
        {
            removeStart = before;
        }
        else
        {
            int after = SkipWhitespace(json, valueEnd);
            if (after < json.Length && json[after] == ',')
            {
                removeEnd = after + 1;
            }
        }

        return CarrierMatch.Found;
    }

    // Decodes the JSON string literal, strips it if it contains a JSON object, re-encodes it.
    // Returns null if nothing changed.
    private static string? StripEncodedJson(string literal, int depth)
    {
        string? decoded;
        try
        {
            // Use Newtonsoft to decode the JSON string literal (handles all escape sequences)
            decoded = JsonHelper.DeserializeObject<string>(literal);
        }
        catch (Exception)
        {
            return null;
        }

        if (!IsObjectText(decoded))
        {
            return null;
        }

        var stripped = StripJson(decoded, depth + 1);
        if (ReferenceEquals(stripped, decoded))
        {
            return null;
        }

        // Re-encode as a JSON string literal
        return JsonHelper.SerializeObject(stripped);
    }

    // Plain customer text such as "{...} trailing text" is not treated as encoded JSON.
    private static bool IsObjectText([NotNullWhen(true)] string? text)
    {
        if (text == null)
        {
            return false;
        }

        int first = SkipWhitespace(text, 0);
        if (first >= text.Length || text[first] != '{')
        {
            return false;
        }

        return text[SkipWhitespaceBackward(text, text.Length - 1)] == '}';
    }

    // Returns index just past the closing quote of the string opening at start.
    private static int StringEnd(string json, int start)
    {
        for (int i = start + 1; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '\\')
            {
                i++;
            }
            else if (c == '"')
            {
                return i + 1;
            }
        }

        return -1;
    }

    // Returns index just past the closing brace of the object opening at start.
    private static int ObjectEnd(string json, int start)
    {
        int depth = 0;
        for (int i = start; i < json.Length; i++)
        {
            char c = json[i];
            if (c == '"')
            {
                int end = StringEnd(json, i);
                if (end < 0)
                {
                    return -1;
                }

                i = end - 1;
            }
            else if (c == '{')
            {
                depth++;
            }
            else if (c == '}' && --depth == 0)
            {
                return i + 1;
            }
        }

        return -1;
    }

    // Object keys are customer property names and are never rewritten.
    private static bool IsKey(string json, int stringEnd)
    {
        int next = SkipWhitespace(json, stringEnd);
        return next < json.Length && json[next] == ':';
    }

    private static int SkipWhitespace(string json, int i)
    {
        while (i < json.Length && IsWhitespace(json[i]))
        {
            i++;
        }

        return i;
    }

    private static int SkipWhitespaceBackward(string json, int i)
    {
        while (i >= 0 && IsWhitespace(json[i]))
        {
            i--;
        }

        return i;
    }

    private static bool IsWhitespace(char c)
        => c is ' ' or '\t' or '\n' or '\r';
}
#endif
