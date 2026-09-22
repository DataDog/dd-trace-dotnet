// <copyright file="TruncatorTraceProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class TruncatorTraceProcessorTests
    {
        // Mirrors the current trace-agent strict-byte-ceiling behavior
        // (pkg/trace/traceutil/normalize/truncate.go): the result never exceeds the byte limit and is
        // never split mid-code-point. "é" (e-acute) is 2 UTF-8 bytes, so "télé" is 6
        // bytes and @5 truncates to "tél" (4 bytes).
        [Fact]
        public void TruncateString()
        {
            Assert.Equal(string.Empty, TruncateUTF8(string.Empty, 5));
            Assert.Equal("tél", TruncateUTF8("télé", 5));
            Assert.Equal("t", TruncateUTF8("télé", 2));
            Assert.Equal("éé", TruncateUTF8("ééééé", 5));
            Assert.Equal("ééééé", TruncateUTF8("ééééé", 18));
            Assert.Equal("ééééé", TruncateUTF8("ééééé", 10));
            Assert.Equal("ééé", TruncateUTF8("ééééé", 6));

            static string TruncateUTF8(string value, int limit)
            {
                Trace.Processors.TraceUtil.TruncateUTF8(ref value, limit);
                return value;
            }
        }

        [Fact]
        public void TruncateString_NeverSplitsCodePoint()
        {
            // A surrogate-pair code point (U+1F600) is 4 UTF-8 bytes and 2 UTF-16 chars. Truncating at any
            // byte limit that would bisect the second one must drop it whole, never leaving a lone
            // surrogate (which would be invalid UTF-8).
            var twoEmoji = "\U0001F600\U0001F600"; // 8 bytes
            for (var limit = 4; limit < 8; limit++)
            {
                var result = TruncateUTF8(twoEmoji, limit);
                Assert.Equal("\U0001F600", result); // exactly one emoji kept; never a split pair
                AssertValidUtf8(result);
                Assert.True(Encoding.UTF8.GetByteCount(result) <= limit);
            }

            // A base letter + combining acute accent (U+0301): 'e' (1 byte) + U+0301 (2 bytes) = 3 bytes,
            // followed by 'z' (1 byte). Like the trace-agent we cut at a code-point boundary only (no
            // grapheme handling), so at limit 2 the combining mark doesn't fit and we keep the bare base
            // 'e'; at limit 3 the 'e' and its mark both fit.
            var combining = "éz";
            Assert.Equal("e", TruncateUTF8(combining, 2));
            Assert.Equal("é", TruncateUTF8(combining, 3));

            static string TruncateUTF8(string value, int limit)
            {
                Trace.Processors.TraceUtil.TruncateUTF8(ref value, limit);
                return value;
            }

            static void AssertValidUtf8(string value)
            {
                // A round-trip through a throw-on-invalid UTF-8 encoder fails if value contains a lone surrogate.
                var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                strict.GetBytes(value);
            }
        }

        [Fact]
        public void TruncateString_LongAsciiTruncatesToExactByteLimit()
        {
            // A long ASCII string (each char = 1 byte) far over the limit. The `length > limit` fast-path
            // short-circuit skips the byte count and goes straight to the slow path, which must cut to
            // exactly `limit` chars / bytes.
            const int limit = 5000;
            var result = TruncateUTF8(new string('a', 100_000), limit);
            Assert.Equal(limit, result.Length);
            Assert.Equal(limit, Encoding.UTF8.GetByteCount(result));

            static string TruncateUTF8(string value, int limit)
            {
                Trace.Processors.TraceUtil.TruncateUTF8(ref value, limit);
                return value;
            }
        }

        [Fact]
        public void TruncateString_LoneSurrogateDoesNotThrow()
        {
            // A lone high surrogate is malformed UTF-16. Truncation must not throw (the byte count uses a
            // non-throwing encoder that substitutes U+FFFD), and the result must stay within the byte limit.
            // The ASCII padding keeps the value over the limit so it reaches the slow path.
            var result = TruncateUTF8("\uD83D" + new string('a', 300), 200);
            Assert.True(result.Length <= 200);
            Assert.True(Encoding.UTF8.GetByteCount(result) <= 200);

            static string TruncateUTF8(string value, int limit)
            {
                Trace.Processors.TraceUtil.TruncateUTF8(ref value, limit);
                return value;
            }
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate_test.go#L25-L38
        [Fact]
        public void TruncateResourceTest()
        {
            Assert.Equal("resource", Trace.Processors.TruncatorTraceProcessor.TruncateResource("resource"));

            var s = new string('a', Trace.Processors.TruncatorTraceProcessor.MaxResourceLen);
            Assert.Equal(s, Trace.Processors.TruncatorTraceProcessor.TruncateResource(s + "extra string"));
        }
    }
}
