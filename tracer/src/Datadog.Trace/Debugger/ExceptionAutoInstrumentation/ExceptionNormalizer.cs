// <copyright file="ExceptionNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.VendoredMicrosoftCode.System;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;
using MemoryExtensions = Datadog.Trace.Debugger.Helpers.MemoryExtensions;
#pragma warning disable SA1005

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ExceptionNormalizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int NormalizeAndHashException(string exceptionString, string outerExceptionType, string? innerExceptionType)
        {
            if (string.IsNullOrEmpty(exceptionString))
            {
                throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
            }

            var fnvHashCode = Fnv1aHash.FnvOffsetBias;

            fnvHashCode = HashLine(VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(outerExceptionType), fnvHashCode);

            if (innerExceptionType != null)
            {
                fnvHashCode = HashLine(VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(innerExceptionType), fnvHashCode);
            }

            var exceptionSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(exceptionString);

            while (!exceptionSpan.IsEmpty)
            {
                var lineEndIndex = exceptionSpan.IndexOfAny('\r', '\n');
                VendoredMicrosoftCode.System.ReadOnlySpan<char> line;

                if (lineEndIndex >= 0)
                {
                    line = exceptionSpan.Slice(0, lineEndIndex);
                    exceptionSpan = exceptionSpan.Slice(lineEndIndex + 1);
                    if (!exceptionSpan.IsEmpty && exceptionSpan[0] == '\n')
                    {
                        exceptionSpan = exceptionSpan.Slice(1);
                    }
                }
                else
                {
                    line = exceptionSpan;
                    exceptionSpan = default;
                }

                if (IsStackTraceLine(line))
                {
                    var inSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(" in ");
                    var index = VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(line, inSpan);
                    fnvHashCode = HashLine(index > 0 ? line.Slice(0, index) : line, fnvHashCode);
                }
            }

            return fnvHashCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsStackTraceLine(VendoredMicrosoftCode.System.ReadOnlySpan<char> line)
        {
            int i = 0;

            while (i < line.Length && char.IsWhiteSpace(line[i]))
            {
                i++;
            }

            return line.Length - i >= 3 && line[i] == 'a' && line[i + 1] == 't' && line[i + 2] == ' ';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsEndOfStackTrace(VendoredMicrosoftCode.System.ReadOnlySpan<char> line)
        {
            var end1 = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("--- End of stack trace from previous location ---");
            var end2 = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("--- End of inner exception stack trace ---");
            return VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, end1, StringComparison.Ordinal) ||
                   VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, end2, StringComparison.Ordinal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HashLine(VendoredMicrosoftCode.System.ReadOnlySpan<char> line, int fnvHashCode)
        {
            for (var i = 0; i < line.Length; i++)
            {
                fnvHashCode = Fnv1aHash.Combine((uint)line[i], fnvHashCode);
            }

            return fnvHashCode;
        }

        internal static int NormalizeAndHashException2(string exceptionString, string outerExceptionType, string? innerExceptionType)
        {
            if (string.IsNullOrEmpty(exceptionString))
            {
                throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
            }

            CompactException compact = ParseException(VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(exceptionString), VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(outerExceptionType), innerExceptionType != default ? VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(innerExceptionType) : default);
            return HashCompactException(compact);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe int HashCompactException(in CompactException exception)
        {
            int hash = Fnv1aHash.FnvOffsetBias;
            hash = Fnv1aHash.Combine((uint)exception.OuterTypeHash, hash);
            hash = Fnv1aHash.Combine((uint)exception.InnerTypeHash, hash);

            fixed (byte* frames = exception.Frames)
            {
                for (int i = 0; i < exception.FrameCount * CompactException.FrameSize; i++)
                {
                    hash = Fnv1aHash.Combine(frames[i], hash);
                }
            }

            return hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static CompactException ParseException(VendoredMicrosoftCode.System.ReadOnlySpan<char> exceptionString, VendoredMicrosoftCode.System.ReadOnlySpan<char> outerExceptionType, VendoredMicrosoftCode.System.ReadOnlySpan<char> innerExceptionType)
        {
            var result = new CompactException
            {
                OuterTypeHash = FastHash(outerExceptionType),
                InnerTypeHash = innerExceptionType.IsEmpty ? 0 : FastHash(innerExceptionType)
            };

            var lineStart = 0;

            //var debug = Datadog.Trace.Util.StringBuilderCache.Acquire(Datadog.Trace.Util.StringBuilderCache.MaxBuilderSize);

            for (int i = 0; i < exceptionString.Length; i++)
            {
                if (exceptionString[i] == '\n' || i == exceptionString.Length - 1)
                {
                    var line = exceptionString.Slice(lineStart, i - lineStart + (i == exceptionString.Length - 1 ? 1 : 0));
                    line = TrimEfficiently(line);

                    var atSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at ");

                    if (VendoredMicrosoftCode.System.MemoryExtensions.StartsWith(line, atSpan))
                    {
                        var inspan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(" in ");
                        int inIndex = VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(line, inspan);

                        // Debug
                        //var cleansedLine = inIndex > 0 ? line.Slice(3, inIndex - 3) : line.Slice(3);
                        //debug.Append(cleansedLine.ToString() + "\n");

                        result.AddFrame(inIndex > 0 ? line.Slice(3, inIndex - 3) : line.Slice(3));
                    }

                    lineStart = i + 1;
                }
            }

            //var finalStringBeingHashed = Datadog.Trace.Util.StringBuilderCache.GetStringAndRelease(debug);
            //Console.WriteLine(finalStringBeingHashed);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static VendoredMicrosoftCode.System.ReadOnlySpan<char> TrimEfficiently(VendoredMicrosoftCode.System.ReadOnlySpan<char> span)
        {
            int start = 0;
            int end = span.Length - 1;

            while (start < span.Length && char.IsWhiteSpace(span[start]))
            {
                start++;
            }

            while (end >= start && char.IsWhiteSpace(span[end]))
            {
                end--;
            }

            return span.Slice(start, end - start + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FastHash(VendoredMicrosoftCode.System.ReadOnlySpan<char> data)
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (var i = 0; i < data.Length; i++)
                {
                    hash = (hash ^ data[i]) * p;
                }

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;

                return hash;
            }
        }

        internal unsafe struct CompactException
        {
            public const int MaxFrames = 20;
            public const int FrameSize = 8;

            public int OuterTypeHash;
            public int InnerTypeHash;
            public byte FrameCount;
            public fixed byte Frames[MaxFrames * FrameSize];

            public void AddFrame(VendoredMicrosoftCode.System.ReadOnlySpan<char> frame)
            {
                if (FrameCount >= MaxFrames)
                {
                    return;
                }

                fixed (byte* framePtr = &Frames[FrameCount * FrameSize])
                {
                    for (var i = 0; i < FrameSize && i < frame.Length; i++)
                    {
                        framePtr[i] = (byte)frame[i];
                    }
                }

                FrameCount++;
            }
        }
    }
}
