// <copyright file="NormalizerTraceProcessorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Datadog.Trace.Tests.TraceProcessors
{
    public class NormalizerTraceProcessorTests
    {
        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L17
        public static IEnumerable<object[]> GetNormalizeTagValues()
        {
            yield return new object[] { "#test_starting_hash", "test_starting_hash" };
            yield return new object[] { "TestCAPSandSuch", "testcapsandsuch" };
            yield return new object[] { "Test Conversion Of Weird !@#$%^&**() Characters", "test_conversion_of_weird_characters" };
            yield return new object[] { "$#weird_starting", "weird_starting" };
            yield return new object[] { "allowed:c0l0ns", "allowed:c0l0ns" };
            yield return new object[] { "1love", "love" };
            yield return new object[] { "ünicöde", "ünicöde" };
            yield return new object[] { "ünicöde:metäl", "ünicöde:metäl" };
            yield return new object[] { "Data🐨dog🐶 繋がっ⛰てて", "data_dog_繋がっ_てて" };
            yield return new object[] { " spaces   ", "spaces" };
            yield return new object[] { " #hashtag!@#spaces #__<>#  ", "hashtag_spaces" };
            yield return new object[] { ":testing", ":testing" };
            yield return new object[] { "_foo", "foo" };
            yield return new object[] { ":::test", ":::test" };
            yield return new object[] { "contiguous_____underscores", "contiguous_underscores" };
            yield return new object[] { "foo_", "foo" };
            yield return new object[] { "\u017Fodd_\u017Fcase\u017F", "\u017Fodd_\u017Fcase\u017F" }; // edge-case
            yield return new object[] { string.Empty, string.Empty };
            yield return new object[] { " ", string.Empty };
            yield return new object[] { "ok", "ok" };
            yield return new object[] { "™Ö™Ö™™Ö™", "ö_ö_ö" };
            yield return new object[] { "AlsO:ök", "also:ök" };
            yield return new object[] { ":still_ok", ":still_ok" };
            yield return new object[] { "___trim", "trim" };
            yield return new object[] { "12.:trim@", ":trim" };
            yield return new object[] { "12.:trim@@", ":trim" };
            yield return new object[] { "fun:ky__tag/1", "fun:ky_tag/1" };
            yield return new object[] { "fun:ky@tag/2", "fun:ky_tag/2" };
            yield return new object[] { "fun:ky@@@tag/3", "fun:ky_tag/3" };
            yield return new object[] { "tag:1/2.3", "tag:1/2.3" };
            yield return new object[] { "---fun:k####y_ta@#g/1_@@#", "fun:k_y_ta_g/1" };
            yield return new object[] { "AlsO:œ#@ö))œk", "also:œ_ö_œk" };
            yield return new object[] { "test\x99\x8f", "test" };
            yield return new object[] { new string('a', 888), new string('a', 200) };

            var sBuilder = new StringBuilder("a");
            for (var i = 0; i < 799; i++)
            {
                sBuilder.Append("🐶");
            }

            sBuilder.Append('b');
            yield return new object[] { sBuilder.ToString(), "a" };
            yield return new object[] { "a\xFFFD", "a" };
            yield return new object[] { "a\xFFFD\xFFFD", "a" };
            yield return new object[] { "a\xFFFD\xFFFDb", "a_b" };
            yield return new object[] { null, string.Empty };
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L100-L119
        public static IEnumerable<object[]> GetNormalizeNameValues()
        {
            yield return new object[] { string.Empty, Trace.Processors.NormalizerTraceProcessor.DefaultSpanName };
            yield return new object[] { "good", "good" };
            yield return new object[] { "Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.Too-Long-.", "Too_Long.Too_Long.Too_Long.Too_Long.Too_Long.Too_Long.Too_Long.Too_Long.Too_Long.Too_Long." };
            yield return new object[] { "bad-name", "bad_name" };
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L134-L153
        public static IEnumerable<object[]> GetNormalizeServiceValues()
        {
            yield return new object[] { string.Empty, Trace.Processors.NormalizerTraceProcessor.DefaultServiceName };
            yield return new object[] { "good", "good" };
            yield return new object[] { "Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.Too$Long$.", "too_long_.too_long_.too_long_.too_long_.too_long_.too_long_.too_long_.too_long_.too_long_.too_long_." };
            yield return new object[] { "bad$service", "bad_service" };
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L17
        [Theory]
        [MemberData(nameof(GetNormalizeTagValues))]
        public void NormalizeTagTests(string inValue, string expectedValue)
        {
            var actualValue = Trace.Processors.TraceUtil.NormalizeTag(inValue);
            Assert.Equal(expectedValue, actualValue);
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L94
        [Theory]
        [MemberData(nameof(GetNormalizeNameValues))]
        public void NormalizeNameTests(string inValue, string expectedValue)
        {
            var actualValue = Trace.Processors.NormalizerTraceProcessor.NormalizeName(inValue);
            Assert.Equal(expectedValue, actualValue);
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/normalize_test.go#L127
        [Theory]
        [MemberData(nameof(GetNormalizeServiceValues))]
        public void NormalizeServiceTests(string inValue, string expectedValue)
        {
            var actualValue = Trace.Processors.NormalizerTraceProcessor.NormalizeService(inValue);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
