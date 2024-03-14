// <copyright file="SpanMetaStructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Formatters;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests.Agent.MessagePack;

public class SpanMetaStructTests
{
    private const string MetaStructStr = "meta_struct";
    private const string DdStackKey = "_dd.stack.exploit";
    private const string StringKey = "StringKey";
    private const string StringValue = "value";
    private static readonly IFormatterResolver FormatterResolver = SpanFormatterResolver.Instance;
    private static List<object> stackData =
                new List<object>()
                {
                    new Dictionary<string, object>()
                    {
                        { "type", "typevalue" },
                        { "language", "dotnet" },
                        { "id", "213123213123213" },
                        {
                            "frames", new List<object>()
                            {
                                new Dictionary<string, object>()
                                {
                                    { "id", "1" },
                                    { "text", "text" },
                                    { "file", "file.cs" },
                                    { "line", 33U },
                                    { "namespace", "testnamespace" },
                                    { "class_name", "class1" },
                                    { "function", "method1" },
                                },
                                new Dictionary<string, object>()
                                {
                                    { "id", "2" },
                                    { "text", "text2" },
                                    { "file", "file2.cs" },
                                    { "line", 55U },
                                    { "namespace", "testnamespace" },
                                    { "class_name", "class2" },
                                    { "function", "method2" },
                                }
                            }
                        }
                    },
                    new Dictionary<string, object>()
                    {
                        { "type", "type2222" },
                        { "language", "dotnet" },
                        { "id", "test55555" },
                        {
                            "frames", new List<object>()
                            {
                                new Dictionary<string, object>()
                                {
                                    { "id", "frameid" },
                                    { "text", "text" },
                                    { "file", "file.cs" },
                                    { "line", 33U },
                                },
                                new Dictionary<string, object>()
                                {
                                    { "id", "frameid2" },
                                    { "text", "text2" },
                                    { "file", "file2.cs" },
                                    { "line", 55U },
                                }
                            }
                        }
                    }
                };

    public static TheoryData<List<Tuple<string, object?>>> Data
    => new()
       {
            new()
            {
                   new(StringKey, StringValue),
                   new(DdStackKey, stackData)
            },
            new()
            {
                   new(StringKey, 4545),
                   new("thirdKey", new List<object> { "test", 44, new List<string> { "test" } }),
                   new(DdStackKey, true),
            },
            new()
            {
                   new(DdStackKey, null)
            }
       };

    [MemberData(nameof(Data))]
    [Theory]
    public static void GivenAEncodedSpanWithMetaStruct_WhenDecoding_ThenMetaStructIsCorrectlyDecoded(List<Tuple<string, object?>> dataToEncode)
    {
        var span = new Span(new SpanContext(5, 6, samplingPriority: null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };
        List<string> visitedKeys = new();

        // We add the elements to the meta struct
        foreach (var item in dataToEncode)
        {
            span.SetMetaStruct(item.Item1, ObjectToByteArray(item.Item2));
        }

        var spanBytes = new byte[] { };
        var spanBuffer = new SpanBuffer(10000, FormatterResolver);
        // We serialize the span
        var serializationResult = spanBuffer.TryWrite(new ArraySegment<Span>(new[] { span }), ref spanBytes);
        serializationResult.Should().Be(SpanBuffer.WriteStatus.Success);

        // Find the offset of the header "meta_struct" in the byte array
        int offset = FindMetaStructSection(spanBytes);
        offset.Should().BeGreaterThan(0);
        offset += MetaStructStr.Length;

        // Read the map header
        var headerLength = MessagePackBinary.ReadMapHeader(spanBytes, offset, out var bytesRead);
        offset += bytesRead;

        // We check every item in the map
        for (var i = 0; i < headerLength; i++)
        {
            offset = CheckDictionaryItem(spanBytes, offset, dataToEncode, visitedKeys);
        }

        visitedKeys.Count.Should().Be(dataToEncode.Count);
    }

    private static int CheckDictionaryItem(byte[] bytes, int offset, List<Tuple<string, object?>> originalData, List<string> visitedKeys)
    {
        // Read the key
        string result = MessagePackBinary.ReadString(bytes, offset, out var size);
        var originalKey = originalData.Find(x => x.Item1 == result);
        originalKey.Should().NotBeNull();
        visitedKeys.Should().NotContain(result);
        visitedKeys.Add(result);
        var expectedValue = originalKey!.Item2;
        offset += size;

        // Read the value of this key
        var newArray = MessagePackBinary.ReadBytes(bytes, offset, out size);
        var newResult = PrimitiveObjectFormatter.Instance.Deserialize(newArray, 0, FormatterResolver, out var readSize);

        // We should read the whole array
        readSize.Should().Be(newArray.Length);
        newResult.Should().BeEquivalentTo(expectedValue);
        offset += size;
        return offset;
    }

    private static int FindMetaStructSection(byte[] bytes)
    {
        var offset = -1;
        int maxIndex = bytes.Length - MetaStructStr.Length;

        for (var i = 0; i <= maxIndex; i++)
        {
            bool matchFound = true;
            for (int j = 0; j < MetaStructStr.Length; j++)
            {
                if (bytes[i + j] != MetaStructStr[j])
                {
                    matchFound = false;
                    break;
                }
            }

            if (matchFound)
            {
                offset = i;
                break;
            }
        }

        return offset;
    }

    private static byte[] ObjectToByteArray(object? value)
    {
        // 256 is the size that the serializer would reserve initially for empty arrays, so we create
        // the buffer with that size to avoid this first resize. If a bigger size is required later, the serializer
        // will resize it.

        var buffer = new byte[256];
        var bytesCopied = PrimitiveObjectFormatter.Instance.Serialize(ref buffer, 0, value, null);
        Array.Resize(ref buffer, bytesCopied);

        return buffer;
    }
}
