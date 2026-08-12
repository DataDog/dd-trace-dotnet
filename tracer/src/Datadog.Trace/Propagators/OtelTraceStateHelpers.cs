// <copyright file="OtelTraceStateHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Util;

namespace Datadog.Trace.Propagators
{
    /// <summary>
    /// String-surgery helpers over the raw content of the W3C tracestate "ot=" list-member
    /// (OpenTelemetry consistent-probability-sampling sub-keys "rv"/"th"). The value is never
    /// decoded into a typed struct: these two helpers are the only code that inspects or
    /// rewrites the "rv"/"th" sub-keys; every other sub-key (recognized or not) round-trips
    /// byte-for-byte through <see cref="SetRvTh"/> in its original order.
    /// </summary>
    internal static class OtelTraceStateHelpers
    {
        private const int MaxRvHexDigits = 14;

        /// <summary>
        /// Finds the "rv" item in the raw "ot=" value (items separated by ';', key/value by ':')
        /// and returns its value parsed as 1-14 lowercase hex digits, or null if absent or malformed.
        /// Never throws.
        /// </summary>
        internal static ulong? ExtractRv(string? raw)
        {
            if (StringUtil.IsNullOrEmpty(raw))
            {
                return null;
            }

            var remaining = raw!.AsSpan();

            while (true)
            {
                var separatorIndex = remaining.IndexOf(';');
                var item = separatorIndex < 0 ? remaining : remaining.Slice(0, separatorIndex);
                var colonIndex = item.IndexOf(':');

                if (colonIndex > 0 && colonIndex < item.Length - 1 && item.Slice(0, colonIndex).Equals("rv".AsSpan(), StringComparison.Ordinal))
                {
                    var rvSlice = item.Slice(colonIndex + 1);
                    return (rvSlice.Length != MaxRvHexDigits) ? null : TryParseLowercaseHex(rvSlice, out var rv) ? rv : null;
                }

                if (separatorIndex < 0)
                {
                    break;
                }

                remaining = remaining.Slice(separatorIndex + 1);
            }

            return null;
        }

        /// <summary>
        /// Drops any existing "rv"/"th" items from <paramref name="raw"/> (whether well-formed
        /// or not), then emits "rv:&lt;14-hex-digits&gt;" (if <paramref name="rv"/> is non-null)
        /// followed by "th:&lt;hex, trailing zero nibbles trimmed&gt;" (if <paramref name="th"/>
        /// is non-null), followed by every other item from <paramref name="raw"/> in its original
        /// order. Returns null when nothing is left to emit.
        /// </summary>
        internal static string? SetRvTh(string? raw, ulong? rv, ulong? th)
        {
            var sb = StringBuilderCache.Acquire();

            try
            {
                if (rv is { } rvValue)
                {
                    sb.Append("rv:").Append(rvValue.ToString("x14"));
                }

                if (th is { } thValue)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(';');
                    }

                    sb.Append("th:").Append(FormatThresholdHex(thValue));
                }

                if (!StringUtil.IsNullOrEmpty(raw))
                {
                    var remaining = raw!.AsSpan();

                    while (true)
                    {
                        var separatorIndex = remaining.IndexOf(';');
                        var item = separatorIndex < 0 ? remaining : remaining.Slice(0, separatorIndex);
                        var colonIndex = item.IndexOf(':');
                        var key = colonIndex > 0 ? item.Slice(0, colonIndex) : item;

                        if (!key.Equals("rv".AsSpan(), StringComparison.Ordinal) && !key.Equals("th".AsSpan(), StringComparison.Ordinal))
                        {
                            if (sb.Length > 0)
                            {
                                sb.Append(';');
                            }

                            sb.Append(item);
                        }

                        if (separatorIndex < 0)
                        {
                            break;
                        }

                        remaining = remaining.Slice(separatorIndex + 1);
                    }
                }

                return sb.Length == 0 ? null : sb.ToString();
            }
            finally
            {
                StringBuilderCache.Release(sb);
            }
        }

        private static string FormatThresholdHex(ulong th)
        {
            // Format as 14 hex digits, then trim trailing zero nibbles.
            // A fully-zero threshold trims to the empty string; represent it as a single "0".
            var hex = th.ToString("x14");
            var trimmed = hex.TrimEnd('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        private static bool TryParseLowercaseHex(ReadOnlySpan<char> value, out ulong result)
        {
            result = 0;
            foreach (var character in value)
            {
                int digit;

                if (character is >= '0' and <= '9')
                {
                    digit = character - '0';
                }
                else if (character is >= 'a' and <= 'f')
                {
                    digit = character - 'a' + 10;
                }
                else
                {
                    return false;
                }

                result = (result << 4) | (uint)digit;
            }

            return true;
        }
    }
}
