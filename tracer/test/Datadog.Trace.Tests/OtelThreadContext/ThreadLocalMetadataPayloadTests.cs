// <copyright file="ThreadLocalMetadataPayloadTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Text;
using Datadog.Trace.OtelThreadContext;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.OtelThreadContext
{
    /// <summary>
    /// The encoded bytes are an inter-process contract, so these assert the wire format explicitly - tags,
    /// lengths and offsets worked out by hand from the protobuf spec - rather than round-tripping through
    /// the same code that produced them.
    /// </summary>
    public class ThreadLocalMetadataPayloadTests
    {
        // (fieldNumber << 3) | 2, the length-delimited wire type
        private const byte AttributesTag = 0x12;   // ProcessContext.attributes, field 2
        private const byte KeyTag = 0x0A;          // KeyValue.key, field 1
        private const byte ValueTag = 0x12;        // KeyValue.value, field 2
        private const byte StringValueTag = 0x0A;  // AnyValue.string_value, field 1
        private const byte ArrayValueTag = 0x2A;   // AnyValue.array_value, field 5
        private const byte ArrayValuesTag = 0x0A;  // ArrayValue.values, field 1

        [Fact]
        public void EncodesTheSchemaVersionAttribute()
        {
            var encoded = ThreadLocalMetadataPayload.Encode([ThreadLocalMetadataPayload.LocalRootSpanIdKey]);

            // KeyValue { key = "threadlocal.schema_version", value = AnyValue { string_value = "tlsdesc_v1_dev" } }
            encoded[0].Should().Be(AttributesTag);
            encoded[1].Should().Be(46, "the KeyValue is 28 bytes of key plus 18 bytes of value");

            encoded[2].Should().Be(KeyTag);
            encoded[3].Should().Be(26);
            Encoding.UTF8.GetString(encoded, 4, 26).Should().Be("threadlocal.schema_version");

            encoded[30].Should().Be(ValueTag);
            encoded[31].Should().Be(16);
            encoded[32].Should().Be(StringValueTag);
            encoded[33].Should().Be(14);
            Encoding.UTF8.GetString(encoded, 34, 14).Should().Be("tlsdesc_v1_dev");
        }

        [Fact]
        public void EncodesTheAttributeKeyMapAsAnArrayOfStrings()
        {
            var encoded = ThreadLocalMetadataPayload.Encode([ThreadLocalMetadataPayload.LocalRootSpanIdKey]);

            // KeyValue { key = "threadlocal.attribute_key_map", value = AnyValue { array_value = [ ... ] } }
            encoded[48].Should().Be(AttributesTag);
            encoded[49].Should().Be(65);

            encoded[50].Should().Be(KeyTag);
            encoded[51].Should().Be(29);
            Encoding.UTF8.GetString(encoded, 52, 29).Should().Be("threadlocal.attribute_key_map");

            encoded[81].Should().Be(ValueTag);
            encoded[82].Should().Be(32);
            encoded[83].Should().Be(ArrayValueTag);
            encoded[84].Should().Be(30);
            encoded[85].Should().Be(ArrayValuesTag);
            encoded[86].Should().Be(28);
            encoded[87].Should().Be(StringValueTag);
            encoded[88].Should().Be(26);
            Encoding.UTF8.GetString(encoded, 89, 26).Should().Be("datadog.local_root_span_id");

            encoded.Should().HaveCount(115, "the two attributes take 48 and 67 bytes");
        }

        [Fact]
        public void KeyIndexZeroIsTheLocalRootSpanId()
        {
            // The record writer tags its only attribute with key index 0, so index 0 of this table has to
            // be datadog.local_root_span_id or readers will mislabel it.
            var encoded = ThreadLocalMetadataPayload.Encode([ThreadLocalMetadataPayload.LocalRootSpanIdKey, "http.route"]);
            var text = Encoding.UTF8.GetString(encoded);

            text.IndexOf("datadog.local_root_span_id", System.StringComparison.Ordinal)
                .Should().BeLessThan(text.IndexOf("http.route", System.StringComparison.Ordinal));
        }

        [Fact]
        public void UsesMultiByteVarIntsForLongerContent()
        {
            // five 26-character keys push the ArrayValue past 127 bytes, so its length no longer fits in a
            // single varint byte
            var keys = Enumerable.Repeat(ThreadLocalMetadataPayload.LocalRootSpanIdKey, 5).ToArray();
            var encoded = ThreadLocalMetadataPayload.Encode(keys);

            var arrayValue = IndexOf(encoded, [ArrayValueTag, 0x96, 0x01]);
            arrayValue.Should().BeGreaterThan(0, "150 must be encoded as the two bytes 0x96 0x01");
        }

        [Fact]
        public void EncodesAnEmptyKeyMap()
        {
            var encoded = ThreadLocalMetadataPayload.Encode([]);

            // still a well-formed, if empty, array: AnyValue { array_value = ArrayValue { } }
            encoded[83].Should().Be(ArrayValueTag);
            encoded[84].Should().Be(0);
            encoded.Should().HaveCount(85);
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (var i = 0; i + needle.Length <= haystack.Length; i++)
            {
                if (haystack.Skip(i).Take(needle.Length).SequenceEqual(needle))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
