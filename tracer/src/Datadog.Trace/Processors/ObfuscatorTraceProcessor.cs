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
            if (span.OpenTelemetrySemanticsEnabled)
            {
                // The resource name is the low-cardinality span name the OpenTelemetry semantic
                // conventions define, not a query, so obfuscating it would only corrupt it. The
                // query itself is reported in "db.query.text", already sanitized. Note that this
                // only covers the client-side pass: the Datadog agent runs its own obfuscator over
                // the resource name of a "sql" span it receives over msgpack.
                return span;
            }

            if (span.Type == "sql" || span.Type == "cassandra")
            {
                span.ResourceName = ObfuscateSqlResource(span.ResourceName);
            }
            else if (span.Type == SpanTypes.Redis)
            {
                span.ResourceName = ObfuscateRedisResource(span.ResourceName);
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

            sqlQuery = RemoveComments(sqlQuery);

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
                            || IsPrefixedLiteral(sqlChars, sequenceStart, sequenceEnd)
                            || IsDollarQuoted(sqlChars, sequenceStart, sequenceEnd))
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

        /// <summary>
        /// Removes the SQL comments from a query. A comment can hold anything the application put
        /// there, so it is dropped rather than replaced with a placeholder, which is also what the
        /// Datadog agent's obfuscator does. Only the two portable forms are recognized: MySQL's
        /// <c>#</c> is left alone because it also introduces a SQL Server temporary table name.
        /// </summary>
        internal static string RemoveComments(string sqlQuery)
        {
            StringBuilder? sb = null;
            var quoted = false;
            var escaped = false;
            var copiedTo = 0;

            for (var i = 0; i < sqlQuery.Length; i++)
            {
                var c = sqlQuery[i];

                if (quoted)
                {
                    if (c == '\'' && !escaped)
                    {
                        quoted = false;
                    }

                    escaped = (c == '\\') & !escaped;
                    continue;
                }

                if (c == '\'')
                {
                    quoted = true;
                    escaped = false;
                    continue;
                }

                if (i + 1 >= sqlQuery.Length)
                {
                    break;
                }

                var next = sqlQuery[i + 1];
                int end;

                if (c == '-' && next == '-')
                {
                    // A line comment runs to the end of the line, or to the end of the query
                    end = sqlQuery.IndexOf('\n', i + 2);
                    end = end < 0 ? sqlQuery.Length : end;
                }
                else if (c == '/' && next == '*')
                {
                    // An unterminated block comment runs to the end of the query
                    end = sqlQuery.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? sqlQuery.Length : end + 2;
                }
                else
                {
                    continue;
                }

                sb ??= StringBuilderCache.Acquire();
                sb.Append(sqlQuery, copiedTo, i - copiedTo);
                copiedTo = end;
                i = end - 1;
            }

            if (sb is null)
            {
                return sqlQuery;
            }

            sb.Append(sqlQuery, copiedTo, sqlQuery.Length - copiedTo);
            return StringBuilderCache.GetStringAndRelease(sb);
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
            // Note: the comparison must not go through Convert, which throws for a char that does
            // not fit an Int16. A query holding one (any CJK character from U+8000, or either half
            // of an emoji surrogate pair) would otherwise abort obfuscation and leave every literal
            // in the query untouched.
            return c < 256 && Splitters.Get(c);
        }

        private static bool IsNumericLiteralPrefix(char c)
        {
            return c < 256 && NumericLiteralPrefix.Get(c);
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

        /// <summary>
        /// Determines whether the sequence is a string literal introduced by a type prefix, which
        /// every SQL dialect we instrument has some form of: hexadecimal (<c>x'..'</c>), national
        /// character (<c>N'..'</c>), escaped (<c>E'..'</c>), bit (<c>B'..'</c>), Oracle quoted
        /// (<c>q'[..]'</c>), and MySQL character set introducers (<c>_utf8'..'</c>). Without this
        /// the literal is left in place, so the value it holds is reported verbatim.
        /// </summary>
        private static bool IsPrefixedLiteral(char[] sqlChars, int start, int end)
        {
            if (sqlChars[end] != '\'')
            {
                return false;
            }

            // The prefix is the identifier characters between the start of the sequence and the
            // opening quote, and there has to be at least one of them and at least one after it.
            for (var i = start; i < end; i++)
            {
                var c = sqlChars[i];
                if (c == '\'')
                {
                    return i > start;
                }

                // | 0x20 converts ASCII characters to lowercase. A digit is only allowed after the
                // first character, so that a numeric literal is not mistaken for a prefix.
                var isLetter = (c | 0x20) is >= 'a' and <= 'z';
                if (!isLetter && c != '_' && !(i > start && c is >= '0' and <= '9'))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the sequence is a PostgreSQL dollar-quoted string (<c>$$..$$</c> or
        /// <c>$tag$..$tag$</c>), which is how a value holding a quote is written. A positional
        /// parameter (<c>$1</c>) does not end with a dollar sign, so it is left alone.
        /// </summary>
        private static bool IsDollarQuoted(char[] sqlChars, int start, int end)
        {
            return sqlChars[start] == '$' && sqlChars[end] == '$' && end > start;
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
