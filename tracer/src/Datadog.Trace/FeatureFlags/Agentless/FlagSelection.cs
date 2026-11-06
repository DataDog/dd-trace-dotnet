// <copyright file="FlagSelection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// A provider's flag selection in canonical form, as the server and every SDK compute it
/// (https://github.com/DataDog/ffe-system-test-data/blob/main/flag-selection/README.md), or the
/// reason it cannot be sent. Immutable.
/// </summary>
internal sealed class FlagSelection : IEquatable<FlagSelection>
{
    internal const string TagsParameterName = "selected_flag_tags";
    internal const string KeyPatternsParameterName = "selected_flag_key_patterns";

    // The server's limits, from the same contract.
    internal const int MaxValues = 31;
    internal const int MaxValueBytes = 200;

    private static readonly CodePointComparer ValueOrder = new();

    private readonly string[] _tags;
    private readonly string[] _keyPatterns;

    private FlagSelection(string[] tags, string[] keyPatterns, MetricTags.FlagSelectionDroppedReason? dropReason)
    {
        _tags = tags;
        _keyPatterns = keyPatterns;
        DropReason = dropReason;
    }

    /// <summary>Gets the selection of a provider that selects nothing, which loads every flag.</summary>
    public static FlagSelection AllFlags { get; } = new([], [], dropReason: null);

    public IReadOnlyList<string> Tags => _tags;

    public IReadOnlyList<string> KeyPatterns => _keyPatterns;

    /// <summary>Gets why the selection cannot be sent, or <c>null</c> when it can.</summary>
    public MetricTags.FlagSelectionDroppedReason? DropReason { get; }

    /// <summary>Gets a value indicating whether there is a selection to send.</summary>
    public bool HasValues => DropReason is null && (_tags.Length > 0 || _keyPatterns.Length > 0);

    /// <summary>Gets a value indicating whether the provider asked for anything but every flag, including a selection that cannot be sent.</summary>
    public bool IsSet => DropReason is not null || _tags.Length > 0 || _keyPatterns.Length > 0;

    /// <summary>
    /// Canonicalizes each list independently: trims Unicode White_Space, drops empty values and
    /// duplicates, and sorts by UTF-8 bytes. Then validates, stopping at the first failure in the
    /// server's order of precedence.
    /// </summary>
    public static FlagSelection Create(string?[]? tags, string?[]? keyPatterns)
    {
        // Malformed values are left out of the canonical lists, which are still kept so that two
        // providers can be compared.
        var malformed = IsMalformed(tags) || IsMalformed(keyPatterns);
        var canonicalTags = Canonicalize(tags);
        var canonicalKeyPatterns = Canonicalize(keyPatterns);

        MetricTags.FlagSelectionDroppedReason? reason = null;
        if (malformed)
        {
            reason = MetricTags.FlagSelectionDroppedReason.MalformedSelection;
        }
        else if (canonicalTags.Length > MaxValues || canonicalKeyPatterns.Length > MaxValues)
        {
            reason = MetricTags.FlagSelectionDroppedReason.TooManyValues;
        }
        else if (AnyLongerThanLimit(canonicalKeyPatterns))
        {
            reason = MetricTags.FlagSelectionDroppedReason.InvalidKeyPattern;
        }
        else if (AnyLongerThanLimit(canonicalTags))
        {
            reason = MetricTags.FlagSelectionDroppedReason.InvalidTag;
        }

        if (reason is null && canonicalTags.Length == 0 && canonicalKeyPatterns.Length == 0)
        {
            return AllFlags;
        }

        return new FlagSelection(canonicalTags, canonicalKeyPatterns, reason);
    }

    public bool Equals(FlagSelection? other)
        => other is not null
        && DropReason == other.DropReason
        && SequenceEqual(_tags, other._tags)
        && SequenceEqual(_keyPatterns, other._keyPatterns);

    public override bool Equals(object? obj) => obj is FlagSelection other && Equals(other);

    public override int GetHashCode()
    {
        var hash = DropReason.GetHashCode();
        foreach (var tag in _tags)
        {
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(tag);
        }

        foreach (var pattern in _keyPatterns)
        {
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(pattern);
        }

        return hash;
    }

    /// <summary>
    /// Returns the selection as query parameters, one per value, tags first. Each value is
    /// percent-encoded per RFC 3986 (https://www.rfc-editor.org/rfc/rfc3986#section-2): only the
    /// unreserved characters stay as they are, and every other UTF-8 byte is written as %XX with
    /// uppercase hex. The server compares the encoded bytes, so <see cref="Uri.EscapeDataString"/>,
    /// whose unreserved set has varied across runtimes, is not used.
    /// </summary>
    internal string ToQuery()
    {
        var builder = new StringBuilder();
        foreach (var tag in _tags)
        {
            AppendParameter(builder, TagsParameterName, tag);
        }

        foreach (var pattern in _keyPatterns)
        {
            AppendParameter(builder, KeyPatternsParameterName, pattern);
        }

        return builder.ToString();
    }

    private static void AppendParameter(StringBuilder builder, string name, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append('&');
        }

        builder.Append(name).Append('=');
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('%').Append(HexConverter.ToCharUpper(b >> 4)).Append(HexConverter.ToCharUpper(b));
            }
        }
    }

    private static bool IsMalformed(string?[]? values)
    {
        if (values is null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (value is null || HasUnpairedSurrogate(value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnpairedSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(value[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Canonicalize(string?[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return [];
        }

        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (value is not null && TrimWhiteSpace(value) is { Length: > 0 } trimmed)
            {
                unique.Add(trimmed);
            }
        }

        var canonical = new string[unique.Count];
        unique.CopyTo(canonical);
        Array.Sort(canonical, ValueOrder);
        return canonical;
    }

    private static string TrimWhiteSpace(string value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsWhiteSpace(value[start]))
        {
            start++;
        }

        while (end > start && IsWhiteSpace(value[end - 1]))
        {
            end--;
        }

        return value.Substring(start, end - start);
    }

    // The Unicode White_Space property (https://www.unicode.org/Public/UCD/latest/ucd/PropList.txt),
    // which the server trims. char.IsWhiteSpace follows the runtime's own Unicode version instead.
    private static bool IsWhiteSpace(char c)
        => c is (>= '\u0009' and <= '\u000D') or ' ' or '\u0085' or '\u00A0' or '\u1680'
             or (>= '\u2000' and <= '\u200A') or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000';

    private static bool AnyLongerThanLimit(string[] values)
    {
        foreach (var value in values)
        {
            if (Encoding.UTF8.GetByteCount(value) > MaxValueBytes)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SequenceEqual(string[] left, string[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var i = 0; i < left.Length; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Orders strings by code point, which is the order of their UTF-8 bytes. An ordinal comparison
    /// orders UTF-16 code units instead, which puts characters outside the Basic Multilingual Plane
    /// before U+E000 to U+FFFF.
    /// </summary>
    private sealed class CodePointComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x is null || y is null)
            {
                return x is null ? (y is null ? 0 : -1) : 1;
            }

            var length = Math.Min(x.Length, y.Length);
            for (var i = 0; i < length; i++)
            {
                if (x[i] != y[i])
                {
                    return CodePointRank(x[i]) - CodePointRank(y[i]);
                }
            }

            return x.Length - y.Length;
        }

        // Moves surrogates above U+E000 to U+FFFF, which turns code unit order into code point order.
        private static int CodePointRank(char c)
            => c < '\uD800' ? c : c >= '\uE000' ? c - 0x800 : c + 0x2000;
    }
}
