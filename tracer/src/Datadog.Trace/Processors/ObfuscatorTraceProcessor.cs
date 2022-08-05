// <copyright file="ObfuscatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Processors
{
    // https://github.com/DataDog/dd-trace-java/blob/35487fa08f16503105b2ff37fb084ffa5c894f24/internal-api/src/main/java/datadog/trace/api/normalize/SQLNormalizer.java

    internal class ObfuscatorTraceProcessor : ITraceProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ObfuscatorTraceProcessor>();
        private static readonly BitArray NumericLiteralPrefix = new BitArray(256, false);
        private static readonly BitArray Splitters = new BitArray(256, false);
        private readonly ObfuscatorTagsProcessor _tagsProcessor;

        static ObfuscatorTraceProcessor()
        {
            var numericLiterals = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '+', '.' };
            var splitterChars = new[] { ',', '(', ')', '|' };

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

        public ObfuscatorTraceProcessor(bool redisTagObfuscationEnabled)
        {
            _tagsProcessor = new(redisTagObfuscationEnabled);
            Log.Information("ObfuscatorTraceProcessor initialized. Redis tag obfuscation enabled: {RedisObfuscation}", redisTagObfuscationEnabled);
        }

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            for (var i = trace.Offset; i < trace.Count + trace.Offset; i++)
            {
                trace.Array[i] = Process(trace.Array[i]);
            }

            return trace;
        }

        public Span Process(Span span)
        {
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

        public ITagProcessor GetTagProcessor() => _tagsProcessor;

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
                    return new string(sqlChars, 0, outputLength);
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Error obfuscating sql {}", sqlQuery, ex);
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
