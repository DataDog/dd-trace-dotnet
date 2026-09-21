// <copyright file="ThreadLocalMetadataPayload.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Runtime.CompilerServices;
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
/// <para>
/// The whole fragment is measured and then written once into a single array: protobuf length-delimited
/// fields need their length before their content, and because every length here is deterministic we can
/// compute it up front rather than building the payload out of nested growable buffers. Constant strings
/// are held as UTF-8 literals so encoding them never allocates.
/// </para>
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

    // UTF-8 forms of the constant strings above. These must stay byte-for-byte identical to their string
    // counterparts (the wire-format tests assert both), but keeping them as u8 literals means encoding the
    // common payload never allocates a throwaway byte[].
    internal static ReadOnlySpan<byte> SchemaVersionAttributeUtf8 => "threadlocal.schema_version"u8;

    private static ReadOnlySpan<byte> AttributeKeyMapAttributeUtf8 => "threadlocal.attribute_key_map"u8;

    private static ReadOnlySpan<byte> SchemaVersionUtf8 => "tlsdesc_v1_dev"u8;

    /// <summary>
    /// Encodes the two <c>threadlocal.*</c> attributes - <c>schema_version</c> and <c>attribute_key_map</c> -
    /// ready to append to an encoded <c>ProcessContext</c>. Two attributes are always emitted regardless of
    /// how many keys <paramref name="attributeKeys"/> holds; the keys are the <i>contents</i> of the
    /// <c>attribute_key_map</c> array.
    /// </summary>
    /// <param name="attributeKeys">
    /// The attribute key table, in index order. Index 0 must be <see cref="LocalRootSpanIdKey"/>, because
    /// that index is what a thread context record uses to tag its local root span id.
    /// </param>
    public static byte[] Encode(ReadOnlySpan<string> attributeKeys)
    {
        // Pass 1: measure. AnyValue content sizes drive every enclosing length prefix.
        var schemaValueSize = FieldSize(StringValueFieldNumber, SchemaVersionUtf8.Length);

        var arrayContentSize = 0;
        foreach (var key in attributeKeys)
        {
            arrayContentSize += FieldSize(ArrayValuesFieldNumber, FieldSize(StringValueFieldNumber, Utf8ByteCount(key)));
        }

        var keyMapValueSize = FieldSize(ArrayValueFieldNumber, arrayContentSize);

        var size = AttributeSize(SchemaVersionAttributeUtf8.Length, schemaValueSize)
                 + AttributeSize(AttributeKeyMapAttributeUtf8.Length, keyMapValueSize);

        // Pass 2: write. Single allocation - the array we return.
        var buffer = new byte[size];
        var offset = 0;

        // schema_version = AnyValue { string_value = "tlsdesc_v1_dev" }
        WriteAttributeHeader(buffer, ref offset, SchemaVersionAttributeUtf8, schemaValueSize);
        WriteBytesField(buffer, ref offset, StringValueFieldNumber, SchemaVersionUtf8);

        // attribute_key_map = AnyValue { array_value = ArrayValue { values = [ AnyValue { string_value } ... ] } }
        WriteAttributeHeader(buffer, ref offset, AttributeKeyMapAttributeUtf8, keyMapValueSize);
        WriteTag(buffer, ref offset, ArrayValueFieldNumber);
        WriteVarInt(buffer, ref offset, (uint)arrayContentSize);
        foreach (var key in attributeKeys)
        {
            WriteTag(buffer, ref offset, ArrayValuesFieldNumber);
            WriteVarInt(buffer, ref offset, (uint)FieldSize(StringValueFieldNumber, Utf8ByteCount(key)));
            WriteStringField(buffer, ref offset, StringValueFieldNumber, key);
        }

        return buffer;
    }

    /// <summary>
    /// Size of one <c>ProcessContext.attributes</c> entry: a <c>KeyValue</c> whose key is
    /// <paramref name="keyLength"/> bytes and whose value wraps an AnyValue of <paramref name="valueSize"/> bytes.
    /// </summary>
    private static int AttributeSize(int keyLength, int valueSize)
    {
        var keyValueSize = FieldSize(KeyValueKeyFieldNumber, keyLength)
                         + FieldSize(KeyValueValueFieldNumber, valueSize);
        return FieldSize(AttributesFieldNumber, keyValueSize);
    }

    /// <summary>
    /// Writes everything up to (but not including) an attribute's AnyValue content: the outer
    /// <c>attributes</c> header, the <c>key</c>, and the <c>value</c> header. The caller writes the AnyValue
    /// content next, exactly <paramref name="valueSize"/> bytes of it.
    /// </summary>
    private static void WriteAttributeHeader(byte[] buffer, ref int offset, ReadOnlySpan<byte> key, int valueSize)
    {
        var keyValueSize = FieldSize(KeyValueKeyFieldNumber, key.Length)
                         + FieldSize(KeyValueValueFieldNumber, valueSize);

        WriteTag(buffer, ref offset, AttributesFieldNumber);
        WriteVarInt(buffer, ref offset, (uint)keyValueSize);
        WriteBytesField(buffer, ref offset, KeyValueKeyFieldNumber, key);
        WriteTag(buffer, ref offset, KeyValueValueFieldNumber);
        WriteVarInt(buffer, ref offset, (uint)valueSize);
    }

    /// <summary>
    /// Total encoded size of a length-delimited field: tag + length prefix + <paramref name="contentLength"/> bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FieldSize(int fieldNumber, int contentLength)
        => TagSize(fieldNumber) + VarIntSize((uint)contentLength) + contentLength;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int TagSize(int fieldNumber)
        => VarIntSize((uint)((fieldNumber << 3) | LengthDelimited));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Utf8ByteCount(string value) => Encoding.UTF8.GetByteCount(value);

    private static void WriteBytesField(byte[] buffer, ref int offset, int fieldNumber, ReadOnlySpan<byte> content)
    {
        WriteTag(buffer, ref offset, fieldNumber);
        WriteVarInt(buffer, ref offset, (uint)content.Length);
        content.CopyTo(buffer.AsSpan(offset));
        offset += content.Length;
    }

    private static void WriteStringField(byte[] buffer, ref int offset, int fieldNumber, string value)
    {
        WriteTag(buffer, ref offset, fieldNumber);
        WriteVarInt(buffer, ref offset, (uint)Utf8ByteCount(value));

        // The GetBytes(string, int, int, byte[], int) overload writes straight into the destination on
        // every supported runtime (including .NET Framework), so there is no intermediate array.
        offset += Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, offset);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTag(byte[] buffer, ref int offset, int fieldNumber)
        => WriteVarInt(buffer, ref offset, (uint)((fieldNumber << 3) | LengthDelimited));

    private static void WriteVarInt(byte[] buffer, ref int offset, uint value)
    {
        while (value >= 0x80)
        {
            buffer[offset++] = (byte)(value | 0x80);
            value >>= 7;
        }

        buffer[offset++] = (byte)value;
    }

    private static int VarIntSize(uint value)
    {
        var size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }

        return size;
    }
}
