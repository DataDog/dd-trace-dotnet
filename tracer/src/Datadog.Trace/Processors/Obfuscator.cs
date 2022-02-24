// <copyright file="Obfuscator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.TraceProcessors
{
    // https://github.com/DataDog/dd-trace-java/blob/35487fa08f16503105b2ff37fb084ffa5c894f24/internal-api/src/main/java/datadog/trace/api/normalize/SQLNormalizer.java

    internal class Obfuscator
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Obfuscator>();
        private static readonly UTF8Encoding Encoding = new UTF8Encoding(false);

        private static BitArray numericLiteralPrefix = new BitArray(255, false);
        private static BitArray splitters = new BitArray(255, false);

        static Obfuscator()
        {
            char[] byteArray1 = new char[13] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '-', '+', '.' };
            char[] byteArray2 = new char[4] { ',', '(', ')', '|' };

            for (int i = 0; i < byteArray1.Length; ++i)
            {
                int unsigned = byteArray1[i] & 0xFF;
                numericLiteralPrefix.Set(unsigned, true);
            }

            for (int i = 0; i < byteArray2.Length; ++i)
            {
                int unsigned = byteArray2[i] & 0xFF;
                splitters.Set(unsigned, true);
            }

            for (int i = 0; i < 256; ++i)
            {
                if (char.IsWhiteSpace((char)i))
                {
                    splitters.Set(i, true);
                }
            }
        }

        public static string SqlObfuscator(string sql)
        {
            var utf8 = Encoding.GetBytes(sql);

            try
            {
                BitArray splitterBytes = FindSplitterPositions(utf8);
                var outputLength = utf8.Length;
                var end = outputLength;
                var start = end > 0 ? PreviousSetBit(splitterBytes, end - 1) : -1;
                var modified = false;
                var questionMarkByte = Convert.ToByte('?');

                // strip out anything ending with a quote (covers string and hex literals)
                // or anything starting with a number, a quote, a decimal point, or a sign
                while (end > 0 && start > 0)
                {
                    int sequenceStart = start + 1;
                    int sequenceEnd = end - 1;
                    if (sequenceEnd == sequenceStart)
                    {
                        // single digit numbers can can be fixed in place
                        if (char.IsDigit(Convert.ToChar(utf8[sequenceStart])))
                        {
                            utf8[sequenceStart] = questionMarkByte;
                            modified = true;
                        }
                    }
                    else if (sequenceStart < sequenceEnd)
                    {
                        if (IsQuoted(utf8, sequenceStart, sequenceEnd)
                            || IsNumericLiteralPrefix(utf8[sequenceStart])
                            || IsHexLiteralPrefix(utf8, sequenceStart, sequenceEnd))
                        {
                            int length = sequenceEnd - sequenceStart;
                            Array.Copy(utf8, end, utf8, sequenceStart + 1, outputLength - end);
                            utf8[sequenceStart] = questionMarkByte;
                            outputLength -= length;
                            modified = true;
                        }
                    }

                    end = start;
                    start = PreviousSetBit(splitterBytes, start - 1);
                }

                if (modified)
                {
                    byte[] byteArray = new byte[outputLength];
                    Array.Copy(utf8, byteArray, outputLength);

                    return Encoding.GetString(byteArray);
                }
            }
            catch (Exception paranoid)
            {
                Log.Debug("Error obfuscating sql {}", sql, paranoid);
            }

            // return UTF8BytesString.create(sql, utf8);
            Console.WriteLine("sql: " + sql + "utf8: " + utf8);

            if (utf8 is null)
            {
                return null;
            }
            else
            {
                return sql;
            }
        }

        private static BitArray FindSplitterPositions(byte[] utf8)
        {
            var positions = new BitArray(utf8.Length);

            var quoted = false;
            var escaped = false;

            for (int i = 0; i < utf8.Length; ++i)
            {
                byte b = utf8[i];
                if (b == '\'' && !escaped)
                {
                    quoted = !quoted;
                }
                else
                {
                    escaped = (b == '\\') & !escaped;
                    positions.Set(i, !quoted & IsSplitter(b));
                }
            }

            return positions;
        }

        private static bool IsSplitter(byte symbol)
        {
            return splitters.Get(symbol & 0xFF);
        }

        private static bool IsQuoted(byte[] utf8, int start, int end)
        {
            return (utf8[start] == '\'' && utf8[end] == '\'');
        }

        private static bool IsNumericLiteralPrefix(byte symbol)
        {
            return numericLiteralPrefix.Get(symbol & 0xFF);
        }

        private static bool IsHexLiteralPrefix(byte[] utf8, int start, int end)
        {
            return (utf8[start] | ' ') == 'x' && start + 1 < end && utf8[start + 1] == '\'';
        }

        public static int PreviousSetBit(BitArray array, int fromIndex)
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
