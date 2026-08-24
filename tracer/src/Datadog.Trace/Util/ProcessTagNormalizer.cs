// <copyright file="ProcessTagNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.Util;

internal static class ProcessTagNormalizer
{
    private const int MaxTagLength = 200;

    private static readonly UTF8Encoding Encoding = new(false);

    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value) || Encoding.GetByteCount(value) == 0)
        {
            return string.Empty;
        }

        char[] charArray = null;
        char[] upArray = null;
        char[] lowArray = null;
        var trim = 0;
        List<int[]> cuts = null;
        var chars = 0;
        var jump = 0;
        var i = 0;

        for (; i < value.Length; i++)
        {
            var c = value[i];
            jump = 1;

            if ((c >= 'a' && c <= 'z') || c == ':')
            {
                chars++;
                goto End;
            }

            if (c >= 'A' && c <= 'Z')
            {
                charArray ??= value.ToCharArray();
                charArray[i] = (char)(value[i] + ('a' - 'A'));
                chars++;
                goto End;
            }

            if (char.IsUpper(c))
            {
                upArray ??= new char[1];
                lowArray ??= new char[1];
                upArray[0] = c;
                lowArray[0] = char.ToLowerInvariant(c);
                if (Encoding.GetByteCount(upArray) == Encoding.GetByteCount(lowArray))
                {
                    charArray ??= value.ToCharArray();
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
                goto End;
            }
            else if (char.IsDigit(c) || c == '.' || c == '/' || c == '-')
            {
                chars++;
            }
            else
            {
                cuts ??= [];

                var count = cuts.Count;
                if (count > 0 && cuts[count - 1][1] >= i)
                {
                    cuts[count - 1][1] += jump;
                }
                else
                {
                    cuts.Add([i, i + jump]);
                }
            }

        End:
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
            return charArray is null
                       ? value.Substring(trim, i + jump - trim)
                       : new string(charArray, trim, i + jump - trim);
        }

        charArray ??= value.ToCharArray();
        var segment = new ArraySlice<char>(charArray, trim, i + jump - trim);
        var delta = trim;
        foreach (var cut in cuts)
        {
            var start = cut[0] - delta;
            var end = cut[1] - delta;

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
}
