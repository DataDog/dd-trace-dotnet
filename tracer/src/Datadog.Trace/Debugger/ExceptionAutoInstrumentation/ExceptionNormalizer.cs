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
            var inSpan = " in ".AsSpan();
            var atSpan = "at ".AsSpan();
            var lambdaSpan = "lambda_".AsSpan();
            var microsoftSpan = "at Microsoft.".AsSpan();
            var systemSpan = "at System.".AsSpan();
            var datadogSpan = "at Datadog.".AsSpan();

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

                // Is frame line (starts with `in `).
                if (line.TrimStart().StartsWith(atSpan, StringComparison.Ordinal))
                {
                    var index = line.IndexOf(inSpan, StringComparison.Ordinal);
                    line = index > 0 ? line.Slice(0, index) : line;

                    if (line.Contains(lambdaSpan, StringComparison.Ordinal) ||
                        line.Contains(microsoftSpan, StringComparison.Ordinal) ||
                        line.Contains(datadogSpan, StringComparison.Ordinal) ||
                        line.Contains(systemSpan, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    fnvHashCode = HashLine(line, fnvHashCode);
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

            // Check if it's a stack frame line (starts with "at ")
            if (!line.StartsWith("at ".AsSpan(), StringComparison.Ordinal))
            {
                return;
            }

            // Skip the "at " prefix
            line = line.Slice(3);

            // Skip lambda and Datadog frames early
            if (ContainsAny(line, "lambda_", "at Datadog."))
            {
                return;
            }

            // Find the " in " marker and truncate if found
            var inIndex = line.IndexOf(" in ".AsSpan(), StringComparison.Ordinal);

            if (inIndex > 0)
            {
                line = line.Slice(0, inIndex);
            }

            // Only create a string when we're sure we want to keep this frame
            results.Add(line.ToString());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ContainsAny(ReadOnlySpan<char> source, string first, string second)
        {
            return source.Contains(first.AsSpan(), StringComparison.Ordinal) ||
                   source.Contains(second.AsSpan(), StringComparison.Ordinal);
        }
    }
}
