// <copyright file="TraceUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    internal class TraceUtil
    {
        // MaxTagLength the maximum length a tag can have
        private const int MaxTagLength = 200;

        private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate.go#L36-L51
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TruncateUTF8(ref string value, int limit)
        {
            if (string.IsNullOrEmpty(value) || Encoding.GetByteCount(value) <= limit)
            {
                return false;
            }

            var charArray = new char[1];
            var length = 0;
            for (var i = 0; i < value.Length - 1; i++)
            {
                charArray[0] = value[i];
                length += Encoding.GetByteCount(charArray, 0, 1);
                if (length > limit)
                {
                    value = value.Substring(0, i);
                    return true;
                }
            }

            return false;
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

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize.go#L213-L216
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAlpha(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize.go#L218-L221
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAlphaNumeric(char c)
        {
            return IsAlpha(c) || (c >= '0' && c <= '9');
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize.go#L223-L274
        public static string NormalizeMetricName(string name, int limit)
        {
            if (name == string.Empty || Encoding.GetByteCount(name) > limit)
            {
                return null;
            }

            var sb = StringBuilderCache.Acquire(name.Length);
            int i = 0;

            // skip non-alphabetic characters
            for (; i < name.Length && !IsAlpha(name[i]); i++) { }

            // if there were no alphabetic characters it wasn't valid
            if (i == name.Length)
            {
                return null;
            }

            for (; i < name.Length; i++)
            {
                char c = name[i];

                if (IsAlphaNumeric(c))
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
