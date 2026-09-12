// <copyright file="ThreadLocalMetadataPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.OtelThreadContext;

/// <summary>
/// Encodes the <c>threadlocal.*</c> entries that OTEP 4947 requires in the OTEP 4719 process context.
/// Without them, a conformant reader never looks for the <c>otel_thread_ctx_v1</c> symbol at all.
/// <para>
/// The output is a fragment, not a whole message: a sequence of <c>ProcessContext.attributes</c> (field 2)
/// entries meant to be <b>appended</b> to an already-encoded <c>ProcessContext</c>. Protobuf defines
/// concatenation as merging, and repeated fields accumulate, so appending is equivalent to having encoded
/// the entries in the first place - and it means we never have to parse the payload we are extending, so
/// fields we do not know about survive untouched.
/// </para>
/// <code>
/// ProcessContext { repeated KeyValue attributes = 2; }
/// KeyValue       { string key = 1; AnyValue value = 2; }
/// AnyValue       { string string_value = 1; ... ArrayValue array_value = 5; }
/// ArrayValue     { repeated AnyValue values = 1; }
/// </code>
/// </summary>
internal static class ThreadLocalMetadataPayload
{
    /// <summary>
    /// Identifies the record format we publish. This is the value libdatadog's own writer uses and the one
    /// current readers match on; OTEP 4947 anticipates renaming it to <c>tls_v1</c>, which must be done in
    /// step with the readers rather than ahead of them.
    /// </summary>
    public const string SchemaVersion = "tlsdesc_v1_dev";

    /// <summary>
    /// Attribute name for key index 0, the only attribute we publish in a thread context record.
    /// </summary>
    public const string LocalRootSpanIdKey = "datadog.local_root_span_id";

    public const string SchemaVersionAttribute = "threadlocal.schema_version";

    public const string AttributeKeyMapAttribute = "threadlocal.attribute_key_map";

    private const int AttributesFieldNumber = 2;      // ProcessContext.attributes
    private const int KeyValueKeyFieldNumber = 1;     // KeyValue.key
    private const int KeyValueValueFieldNumber = 2;   // KeyValue.value
    private const int StringValueFieldNumber = 1;     // AnyValue.string_value
    private const int ArrayValueFieldNumber = 5;      // AnyValue.array_value
    private const int ArrayValuesFieldNumber = 1;     // ArrayValue.values

    private const int LengthDelimited = 2;

    /// <summary>
    /// Encodes the two <c>threadlocal.*</c> attributes, ready to append to an encoded <c>ProcessContext</c>.
    /// </summary>
    /// <param name="attributeKeys">
    /// The attribute key table, in index order. Index 0 must be <see cref="LocalRootSpanIdKey"/>, because
    /// that index is what a thread context record uses to tag its local root span id.
    /// </param>
    public static byte[] Encode(string[] attributeKeys)
    {
        var buffer = new List<byte>(128);

        WriteAttribute(buffer, SchemaVersionAttribute, StringAnyValue(SchemaVersion));
        WriteAttribute(buffer, AttributeKeyMapAttribute, StringArrayAnyValue(attributeKeys));

        return buffer.ToArray();
    }

    private static void WriteAttribute(List<byte> buffer, string key, List<byte> encodedValue)
    {
        var keyValue = new List<byte>(key.Length + encodedValue.Count + 8);
        WriteStringField(keyValue, KeyValueKeyFieldNumber, key);
        WriteLengthDelimitedField(keyValue, KeyValueValueFieldNumber, encodedValue);

        WriteLengthDelimitedField(buffer, AttributesFieldNumber, keyValue);
    }

    private static List<byte> StringAnyValue(string value)
    {
        var anyValue = new List<byte>(value.Length + 4);
        WriteStringField(anyValue, StringValueFieldNumber, value);
        return anyValue;
    }

    private static List<byte> StringArrayAnyValue(string[] values)
    {
        var arrayValue = new List<byte>(64);

        foreach (var value in values)
        {
            WriteLengthDelimitedField(arrayValue, ArrayValuesFieldNumber, StringAnyValue(value));
        }

        var anyValue = new List<byte>(arrayValue.Count + 4);
        WriteLengthDelimitedField(anyValue, ArrayValueFieldNumber, arrayValue);
        return anyValue;
    }

    private static void WriteStringField(List<byte> buffer, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteTag(buffer, fieldNumber);
        WriteVarInt(buffer, (uint)bytes.Length);
        buffer.AddRange(bytes);
    }

    private static void WriteLengthDelimitedField(List<byte> buffer, int fieldNumber, List<byte> content)
    {
        WriteTag(buffer, fieldNumber);
        WriteVarInt(buffer, (uint)content.Count);
        buffer.AddRange(content);
    }

    private static void WriteTag(List<byte> buffer, int fieldNumber)
        => WriteVarInt(buffer, (uint)((fieldNumber << 3) | LengthDelimited));

    private static void WriteVarInt(List<byte> buffer, uint value)
    {
        while (value >= 0x80)
        {
            buffer.Add((byte)(value | 0x80));
            value >>= 7;
        }

        buffer.Add((byte)value);
    }
}
