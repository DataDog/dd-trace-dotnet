// <copyright file="TraceUtil.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Datadog.Trace.TraceProcessors
{
    internal class TraceUtil
    {
        // MaxTagLength the maximum length a tag can have
        private const int MaxTagLength = 200;

        private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/truncate.go#L34-L49
        public static bool TruncateUTF8(ref string value, int limit)
        {
            if (Encoding.GetByteCount(value) <= limit)
            {
                return false;
            }

            var valueCharArray = value.ToCharArray();
            for (var i = 0; i < valueCharArray.Length; i++)
            {
                if (Encoding.GetByteCount(valueCharArray, 0, i) > limit)
                {
                    value = value.Substring(0, i - 1);
                    return true;
                }
            }

            return false;
        }

        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/agent/normalizer.go#L183-L188
        public static bool IsValidStatusCode(string statusCode)
        {
            if (int.TryParse(statusCode, out int code))
            {
                return 100 <= code && code < 600;
            }

            return false;
        }

        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/normalize.go#L93-L204
        public static string NormalizeTag(string value)
        {
            if (Encoding.GetByteCount(value) == 0)
            {
                return string.Empty;
            }

            var charArray = value.ToCharArray();
            char[] upArray = null;
            char[] lowArray = null;
            int trim = 0;
            List<int[]> cuts = null;
            int chars = 0;
            int jump = 0;
            int i = 0;

            for (; i < charArray.Length; i++)
            {
                var c = charArray[i];
                jump = 1;

                if ((c >= 'a' && c <= 'z') || c == ':')
                {
                    chars++;
                    goto end;
                }

                if (c >= 'A' && c <= 'Z')
                {
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
                    break;
                }

                if (chars >= MaxTagLength)
                {
                    break;
                }
            }

            i--;

            if (cuts is null || cuts.Count == 0)
            {
                return new string(charArray, trim, i + jump - trim);
            }

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

        private readonly ref struct ArraySlice<T>
        {
            public readonly T[] Array;
            public readonly int Offset;
            public readonly int Count;

            public ArraySlice(T[] array, int offset, int count)
            {
                Array = array;
                Offset = offset;
                Count = count;
            }

            public T this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return Array[Offset + index];
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    Array[Offset + index] = value;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySlice<T> Slice(int start)
            {
                if (Count - start < 0)
                {
                    ThrowArgumentException();
                }

                return new ArraySlice<T>(Array, Offset + start, Count - start);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ArraySlice<T> Slice(int start, int count)
            {
                if (count > Count - start)
                {
                    ThrowArgumentOutOfRangeException("count");
                }

                return new ArraySlice<T>(Array, Offset + start, count);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CopyTo(ArraySlice<T> other)
            {
                System.Array.Copy(Array, Offset, other.Array, other.Offset, Count);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowArgumentException()
            {
                throw new ArgumentException();
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowArgumentOutOfRangeException(string name)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }
    }
}
