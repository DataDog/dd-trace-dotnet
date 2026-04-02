// <copyright file="ExceptionNormalizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Fnv1aHash = Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal.Hash;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionNormalizer
    {
        private const string LambdaMarker = "lambda_";
        private const string MicrosoftFramePrefix = "Microsoft.";
        private const string SystemFramePrefix = "System.";
        private const string DatadogFramePrefix = "Datadog.";
        private static readonly string[] FramePrefixes =
        [
            "at ",
            "場所 "
        ];

        private static readonly string[] SourceLocationMarkers =
        [
            " in ",
            " 場所 "
        ];

        protected ExceptionNormalizer()
        {
        }

        public static ExceptionNormalizer Instance { get; } = new();

        /// <summary>
        /// Given the string representation of an exception alongside it's FQN of the outer and (potential) inner exception,
        /// this function cleanse the stack trace from error messages, customized information attached to the exception and PDB line info if present.
        /// It returns a hash representing the resulting cleansed exception and inner exceptions.
        /// Used to aggregate same/similar exceptions that only differ by non-relevant bits.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int NormalizeAndHashException(string exceptionString, string outerExceptionType, string? innerExceptionType)
        {
            if (string.IsNullOrEmpty(exceptionString))
            {
                throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
            }

            var fnvHashCode = HashLine(outerExceptionType.AsSpan(), Fnv1aHash.FnvOffsetBias);

            if (innerExceptionType != null)
            {
                fnvHashCode = HashLine(innerExceptionType.AsSpan(), fnvHashCode);
            }

            var exceptionSpan = exceptionString.AsSpan();

            while (!exceptionSpan.IsEmpty)
            {
                var lineEndIndex = exceptionSpan.IndexOfAny('\r', '\n');
                ReadOnlySpan<char> line;

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

                line = line.TrimStart();

                if (TryStripFramePrefix(line, out var frameLine))
                {
                    var index = FindSourceLocationIndex(frameLine);
                    frameLine = index > 0 ? frameLine.Slice(0, index) : frameLine;

                    if (frameLine.Contains(LambdaMarker.AsSpan(), StringComparison.Ordinal) ||
                        frameLine.StartsWith(MicrosoftFramePrefix.AsSpan(), StringComparison.Ordinal) ||
                        frameLine.StartsWith(DatadogFramePrefix.AsSpan(), StringComparison.Ordinal) ||
                        frameLine.StartsWith(SystemFramePrefix.AsSpan(), StringComparison.Ordinal))
                    {
                        continue;
                    }

                    fnvHashCode = HashLine(frameLine, fnvHashCode);
                }
            }

            return fnvHashCode;
        }

        protected virtual int HashLine(ReadOnlySpan<char> line, int fnvHashCode)
        {
            for (var i = 0; i < line.Length; i++)
            {
                fnvHashCode = Fnv1aHash.Combine((uint)line[i], fnvHashCode);
            }

            return fnvHashCode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal List<string> ParseFrames(string exceptionString)
        {
            if (string.IsNullOrEmpty(exceptionString))
            {
                throw new ArgumentException(@"Exception string cannot be null or empty", nameof(exceptionString));
            }

            var results = new List<string>();
            var currentSpan = exceptionString.AsSpan();

            while (!currentSpan.IsEmpty)
            {
                var lineEndIndex = currentSpan.IndexOfAny('\r', '\n');
                ReadOnlySpan<char> line;

                if (lineEndIndex >= 0)
                {
                    line = currentSpan.Slice(0, lineEndIndex);
                    currentSpan = currentSpan.Slice(lineEndIndex + 1);
                    if (!currentSpan.IsEmpty && currentSpan[0] == '\n')
                    {
                        currentSpan = currentSpan.Slice(1);
                    }
                }
                else
                {
                    line = currentSpan;
                    currentSpan = default;
                }

                ProcessLine(line, results);
            }

            return results;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessLine(ReadOnlySpan<char> line, List<string> results)
        {
            line = line.TrimStart();
            if (line.IsEmpty)
            {
                return;
            }

            if (!TryStripFramePrefix(line, out line))
            {
                return;
            }

            if (line.Contains(LambdaMarker.AsSpan(), StringComparison.Ordinal) ||
                line.StartsWith(DatadogFramePrefix.AsSpan(), StringComparison.Ordinal))
            {
                return;
            }

            var inIndex = FindSourceLocationIndex(line);

            if (inIndex > 0)
            {
                line = line.Slice(0, inIndex);
            }

            // Only create a string when we're sure we want to keep this frame
            results.Add(line.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryStripFramePrefix(ReadOnlySpan<char> line, out ReadOnlySpan<char> frameLine)
        {
            for (var i = 0; i < FramePrefixes.Length; i++)
            {
                var prefix = FramePrefixes[i].AsSpan();
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    frameLine = line.Slice(prefix.Length);
                    return true;
                }
            }

            frameLine = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindSourceLocationIndex(ReadOnlySpan<char> line)
        {
            for (var i = 0; i < SourceLocationMarkers.Length; i++)
            {
                var index = line.IndexOf(SourceLocationMarkers[i].AsSpan(), StringComparison.Ordinal);
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
