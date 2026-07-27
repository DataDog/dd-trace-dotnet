// <copyright file="TraceUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    internal static class TraceUtil
    {
        // MaxTagLength the maximum length a tag can have
        private const int MaxTagLength = 200;

        private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

        /// <summary>
        /// Truncates <paramref name="value"/> so its UTF-8 byte length is at most <paramref name="limit"/>,
        /// cutting at a code-point boundary so it never splits a UTF-8 multi-byte sequence (UTF-16 surrogate
        /// pair). Matches the current trace-agent's strict-byte-ceiling behavior.
        /// </summary>
        /// <returns>true if the value was truncated. false otherwise</returns>
        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate.go#L36-L51
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TruncateUTF8(ref string value, int limit)
        {
            // Each UTF-16 char is at most 3 UTF-8 bytes, so a string of length <= limit/3 can never exceed the byte limit.
            if (string.IsNullOrEmpty(value) || value.Length <= limit / 3)
            {
                return false;
            }

            // Each char is at least 1 byte, so length > limit guarantees we're over the limit.
            // We only count when length <= limit, where we need to count up to limit chars.
            if (value.Length <= limit && Encoding.GetByteCount(value) <= limit)
            {
                return false;
            }

            // Extracted as uncommon slow path, to aid inlining
            value = TruncateUTF8Slow(value, limit);
            return true;

            static string TruncateUTF8Slow(string value, int limit)
            {
                // Binary search to find the cutoff point
                // Find the largest prefix whose UTF-8 byte count is <= limit by counting only (never
                // materializing UTF-8 bytes), then cut there. Seed lo at limit/3 (a limit/3-char prefix
                // always fits, since each char is <= 3 bytes) and cap hi at limit (the cut can't exceed
                // `limit` chars, since each char is >= 1 byte).
                var lo = limit / 3;
                var hi = Math.Min(value.Length, limit);
#if NETCOREAPP
                while (lo < hi)
                {
                    // Bias the midpoint up so lo strictly advances and the loop terminates.
                    var mid = lo + ((hi - lo + 1) >> 1);
                    if (Encoding.GetByteCount(value.AsSpan(0, mid)) <= limit)
                    {
                        lo = mid;
                    }
                    else
                    {
                        hi = mid - 1;
                    }
                }
#else
                // .NET Framework / netstandard2.0 lack the span GetByteCount overload, and we avoid `fixed`.
                // Copy the bounded prefix (at most `limit` chars) into a pooled buffer and count ranges of it.
                var buffer = ArrayPool<char>.Shared.Rent(hi);
                try
                {
                    value.CopyTo(0, buffer, 0, hi);
                    while (lo < hi)
                    {
                        var mid = lo + ((hi - lo + 1) >> 1);
                        if (Encoding.GetByteCount(buffer, 0, mid) <= limit)
                        {
                            lo = mid;
                        }
                        else
                        {
                            hi = mid - 1;
                        }
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
#endif
                var cut = lo;

                // A count probe can land between a high and low surrogate. Drop a trailing lone high
                // surrogate to snap back to a code-point boundary (this only reduces the byte count, so the
                // result stays within the limit). Like the trace-agent, we cut at a code-point (rune)
                // boundary only - no combining-mark / grapheme-cluster handling.
                if (cut > 0 && char.IsHighSurrogate(value[cut - 1]))
                {
                    cut--;
                }

                return value.Substring(0, cut);
            }
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/agent/normalizer.go#L214-L219
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValidStatusCode(string statusCode)
        {
            if (int.TryParse(statusCode, out int code))
            {
                return 100 <= code && code < 600;
            }

            return false;
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize.go#L98-L209
        public static string NormalizeTag(string value)
        {
            if (value is null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(value) || Encoding.GetByteCount(value) == 0)
            {
                return string.Empty;
            }

            char[] charArray = null;
            char[] upArray = null;
            char[] lowArray = null;
            int trim = 0;
            List<int[]> cuts = null;
            int chars = 0;
            int jump = 0;
            int i = 0;

            for (; i < value.Length; i++)
            {
                var c = value[i];
                jump = 1;

                if ((c >= 'a' && c <= 'z') || c == ':')
                {
                    chars++;
                    goto end;
                }

                if (c >= 'A' && c <= 'Z')
                {
                    if (charArray is null)
                    {
                        charArray = value.ToCharArray();
                    }

                    charArray[i] = (char)(((int)value[i]) + ((int)'a' - (int)'A'));
                    chars++;
                    goto end;
                }

                if (char.IsUpper(c))
                {
                    if (upArray is null)
                    {
                        upArray = new char[1];
                    }

                    if (lowArray is null)
                    {
                        lowArray = new char[1];
                    }

                    upArray[0] = c;
                    lowArray[0] = char.ToLowerInvariant(c);
                    if (Encoding.GetByteCount(upArray) == Encoding.GetByteCount(lowArray))
                    {
                        if (charArray is null)
                        {
                            charArray = value.ToCharArray();
                        }

                        charArray[i] = lowArray[0];
                        c = lowArray[0];
                    }
                }

                if (char.IsLetter(c))
                {
                    chars++;
                }
                else if (chars == 0)
                {
                    trim = i + jump;
                    goto end;
                }
                else if (char.IsDigit(c) || c == '.' || c == '/' || c == '-')
                {
                    chars++;
                }
                else
                {
                    if (cuts is null)
                    {
                        cuts = new List<int[]>();
                    }

                    var n = cuts.Count;
                    if (n > 0 && cuts[n - 1][1] >= i)
                    {
                        cuts[n - 1][1] += jump;
                    }
                    else
                    {
                        cuts.Add(new int[] { i, i + jump });
                    }
                }

            end:
                if (i + jump >= 2 * MaxTagLength)
                {
                    i++;
                    break;
                }

                if (chars >= MaxTagLength)
                {
                    i++;
                    break;
                }
            }

            i--;

            if (cuts is null || cuts.Count == 0)
            {
                if (charArray is null)
                {
                    return value.Substring(trim, i + jump - trim);
                }

                return new string(charArray, trim, i + jump - trim);
            }

            charArray ??= value.ToCharArray();
            var segment = new ArraySlice<char>(charArray, trim, i + jump - trim);
            int delta = trim;
            foreach (var cut in cuts)
            {
                int start = cut[0] - delta;
                int end = cut[1] - delta;

                if (end >= segment.Count)
                {
                    segment = segment.Slice(0, start);
                    break;
                }

                segment[start] = '_';
                if (end - start == 1)
                {
                    continue;
                }

                segment.Slice(end).CopyTo(segment.Slice(start + 1));
                segment = segment.Slice(0, segment.Count - (end - start) + 1);
                delta += cut[1] - cut[0] - 1;
            }

            return new string(segment.Array, segment.Offset, segment.Count);
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize.go#L223-L274
        public static string NormalizeMetricName(string name, int limit)
        {
            if (string.IsNullOrEmpty(name) || Encoding.GetByteCount(name) > limit)
            {
                return null;
            }

            var sb = StringBuilderCache.Acquire(name.Length);
            int i = 0;

            // skip non-alphabetic characters
            for (; i < name.Length && !char.IsAsciiLetter(name[i]); i++) { }

            // if there were no alphabetic characters it wasn't valid
            if (i == name.Length)
            {
                return null;
            }

            for (; i < name.Length; i++)
            {
                char c = name[i];

                if (char.IsAsciiLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == '.')
                {
                    // we skipped all non-alpha chars up front so we have seen at least one

                    // overwrite underscores that happen before periods
                    if (sb[sb.Length - 1] == '_')
                    {
                        sb[sb.Length - 1] = '.';
                    }
                    else
                    {
                        sb.Append('.');
                    }
                }
                else
                {
                    // we skipped all non-alpha chars up front so we have seen at least one

                    // no double underscores, no underscores after periods
                    switch (sb[sb.Length - 1])
                    {
                        case '.':
                        case '_':
                        default:
                            sb.Append('_');
                            break;
                    }
                }
            }

            if (sb[sb.Length - 1] == '_')
            {
                sb.Remove(sb.Length - 1, 1);
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}
