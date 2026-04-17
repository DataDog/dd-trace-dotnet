// <copyright file="ObfuscatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Text;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Processors
{
    // https://github.com/DataDog/dd-trace-java/blob/35487fa08f16503105b2ff37fb084ffa5c894f24/internal-api/src/main/java/datadog/trace/api/normalize/SQLNormalizer.java

    internal sealed class ObfuscatorTraceProcessor : ITraceProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ObfuscatorTraceProcessor>();
        private static readonly BitArray NumericLiteralPrefix = new BitArray(256, false);
        private static readonly BitArray Splitters = new BitArray(256, false);

        static ObfuscatorTraceProcessor()
        {
            var numericLiterals = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '+', '.' };
            // Operator characters must act as token splitters to match the Go agent's SQL obfuscation
            // behavior (DataDog/go-sqllexer isOperator function). Without these, queries like
            // WHERE id='1' (no spaces around =) won't have their literals replaced with ?.
            // See: https://github.com/DataDog/go-sqllexer/blob/main/sqllexer_utils.go
            // Note: '+' and '-' are excluded because they are already in numericLiteralPrefix
            // and adding them as splitters would break negative number obfuscation (e.g., col > -123).
            // The Go lexer handles this with look-ahead logic that our simpler approach can't replicate.
            var splitterChars = new[] { ',', '(', ')', '|', '*', '/', '=', '<', '>', '!', '&', '^', '%', '~', '?', '@', ':', '#' };

            foreach (var c in numericLiterals)
            {
                NumericLiteralPrefix[c] = true;
            }

            foreach (var c in splitterChars)
            {
                Splitters.Set(c, true);
            }

            for (var i = 0; i < 256; ++i)
            {
                if (char.IsWhiteSpace(Convert.ToChar(i)))
                {
                    Splitters.Set(i, true);
                }
            }
        }

        public SpanCollection Process(in SpanCollection trace)
        {
            foreach (var span in trace)
            {
                Process(span);
            }

            return trace;
        }

        public Span Process(Span span)
        {
            // TODO: This must happen on span context creation because it affects sampling
            if (span.Type == "sql" || span.Type == "cassandra")
            {
                // span.ResourceName = ObfuscateSqlResource(span.ResourceName);
            }
            else if (span.Type == SpanTypes.Redis)
            {
                // span.ResourceName = ObfuscateRedisResource(span.ResourceName);
            }

            return span;
        }

        public ITagProcessor? GetTagProcessor() => null;

        internal static string ObfuscateSqlResource(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                return string.Empty;
            }

            var sqlChars = sqlQuery.ToCharArray();

            try
            {
                var splitterBytes = FindSplitterPositions(sqlChars);
                var outputLength = sqlChars.Length;
                var end = outputLength;
                var start = PreviousSetBit(splitterBytes, end - 1);
                var modified = false;

                // strip out anything ending with a quote (covers string and hex literals)
                // or anything starting with a number, a quote, a decimal point, or a sign
                while (end > 0 && start > 0)
                {
                    var sequenceStart = start + 1;
                    var sequenceEnd = end - 1;
                    if (sequenceEnd == sequenceStart)
                    {
                        // single digit numbers can can be fixed in place
                        if (char.IsDigit(sqlChars[sequenceStart]))
                        {
                            sqlChars[sequenceStart] = '?';
                            modified = true;
                        }
                    }
                    else if (sequenceStart < sequenceEnd)
                    {
                        if (IsQuoted(sqlChars, sequenceStart, sequenceEnd)
                            || IsNumericLiteralPrefix(sqlChars[sequenceStart])
                            || IsHexLiteralPrefix(sqlChars, sequenceStart, sequenceEnd))
                        {
                            var length = sequenceEnd - sequenceStart;
                            Array.Copy(sqlChars, end, sqlChars, sequenceStart + 1, outputLength - end);
                            sqlChars[sequenceStart] = '?';
                            outputLength -= length;
                            modified = true;
                        }
                    }

                    end = start;
                    start = PreviousSetBit(splitterBytes, start - 1);
                }

                if (modified)
                {
                    // The Go agent normalizes SQL by adding spaces between tokens (via go-sqllexer's
                    // Normalizer). After replacing literals with ?, ensure spaces exist around operator
                    // characters adjacent to ? so that e.g. "id='1'" becomes "id = ?" not "id=?".
                    return NormalizeAroundPlaceholders(sqlChars, outputLength);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Error obfuscating sql {Query}", sqlQuery);
            }

            return sqlQuery;
        }

        internal static string ObfuscateRedisResource(string redisResource)
        {
            if (string.IsNullOrEmpty(redisResource))
            {
                return string.Empty;
            }

            return RedisObfuscationUtil.Quantize(redisResource);
        }

        private static BitArray FindSplitterPositions(char[] sqlChars)
        {
            var positions = new BitArray(sqlChars.Length);

            var quoted = false;
            var escaped = false;

            for (var i = 0; i < sqlChars.Length; ++i)
            {
                var c = sqlChars[i];
                if (c == '\'' && !escaped)
                {
                    quoted = !quoted;
                }
                else
                {
                    escaped = (c == '\\') & !escaped;
                    positions.Set(i, !quoted & IsSplitter(c));
                }
            }

            return positions;
        }

        private static bool IsSplitter(char c)
        {
            if (Convert.ToInt16(c) < 256)
            {
                return Splitters.Get(c);
            }

            return false;
        }

        private static bool IsNumericLiteralPrefix(char c)
        {
            return NumericLiteralPrefix.Get(Convert.ToByte(c));
        }

        /// <summary>
        /// Scans forward through the obfuscated SQL, and when it encounters a run of comparison
        /// operators (=, &lt;, &gt;, !) immediately followed by ?, it ensures spaces exist
        /// around the operator run. For example, "id=?" becomes "id = ?" and "col&lt;>?" becomes "col &lt;> ?".
        /// This matches the Go agent's go-sqllexer Normalizer, which adds spaces between all tokens.
        /// Limited to comparison operators to avoid disrupting concatenation (||?||) and arithmetic patterns.
        /// </summary>
        private static string NormalizeAroundPlaceholders(char[] sqlChars, int length)
        {
            StringBuilder? sb = null;

            for (var i = 0; i < length; i++)
            {
                if (IsComparisonOperator(sqlChars[i]))
                {
                    var opStart = i;
                    var opEnd = i + 1;
                    while (opEnd < length && IsComparisonOperator(sqlChars[opEnd]))
                    {
                        opEnd++;
                    }

                    if (opEnd < length && sqlChars[opEnd] == '?')
                    {
                        // Lazily allocate StringBuilder on first normalization needed,
                        // copying everything we've already scanned past.
                        if (sb is null)
                        {
                            sb = StringBuilderCache.Acquire();
                            sb.Append(sqlChars, 0, i);
                        }

                        // Add space before operator if needed
                        if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
                        {
                            sb.Append(' ');
                        }

                        // Append the operator chars and a trailing space before ?
                        sb.Append(sqlChars, opStart, opEnd - opStart);
                        sb.Append(' ');

                        i = opEnd - 1; // loop will increment to opEnd (the '?')
                        continue;
                    }
                }

                sb?.Append(sqlChars[i]);
            }

            return sb is null ? new string(sqlChars, 0, length) : StringBuilderCache.GetStringAndRelease(sb);
        }

        private static bool IsComparisonOperator(char c)
            => c is '=' or '<' or '>' or '!';

        private static bool IsQuoted(char[] sqlChars, int start, int end)
        {
            return (sqlChars[start] == '\'' && sqlChars[end] == '\'');
        }

        private static bool IsHexLiteralPrefix(char[] sqlChars, int start, int end)
        {
            // | 0x20 converts ASCII characters to lowercase
            return (sqlChars[start] | 0x20) == 'x' && start + 1 < end && sqlChars[start + 1] == '\'';
        }

        private static int PreviousSetBit(BitArray array, int fromIndex)
        {
            if (fromIndex < 0)
            {
                if (fromIndex == -1)
                {
                    return -1;
                }

                throw new IndexOutOfRangeException("Index < -1: " + fromIndex);
            }

            for (var i = fromIndex; i > -1; --i)
            {
                if (array[i])
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
