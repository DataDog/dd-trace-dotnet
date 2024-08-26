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

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal class ExceptionNormalizer
    {
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

            var fnvHashCode = HashLine(VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(outerExceptionType), Fnv1aHash.FnvOffsetBias);

            if (innerExceptionType != null)
            {
                fnvHashCode = HashLine(VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(innerExceptionType), fnvHashCode);
            }

            var exceptionSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(exceptionString);
            var inSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan(" in ");
            var atSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at ");
            var lambdaSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("lambda_");
            var microsoftSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at Microsoft.");
            var systemSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at System.");
            var datadogSpan = VendoredMicrosoftCode.System.MemoryExtensions.AsSpan("at Datadog.");

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

                // Is frame line (starts with `in `).
                if (VendoredMicrosoftCode.System.MemoryExtensions.StartsWith(line.TrimStart(), atSpan, StringComparison.Ordinal))
                {
                    var index = VendoredMicrosoftCode.System.MemoryExtensions.IndexOf(line, inSpan, StringComparison.Ordinal);
                    line = index > 0 ? line.Slice(0, index) : line;

                    if (VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, lambdaSpan, StringComparison.Ordinal) ||
                        VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, microsoftSpan, StringComparison.Ordinal) ||
                        VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, datadogSpan, StringComparison.Ordinal) ||
                        VendoredMicrosoftCode.System.MemoryExtensions.Contains(line, systemSpan, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    fnvHashCode = HashLine(line, fnvHashCode);
                }
            }

            return fnvHashCode;
        }

        protected virtual int HashLine(VendoredMicrosoftCode.System.ReadOnlySpan<char> line, int fnvHashCode)
        {
            for (var i = 0; i < line.Length; i++)
            {
                fnvHashCode = Fnv1aHash.Combine((uint)line[i], fnvHashCode);
            }

            return fnvHashCode;
        }
    }
}
