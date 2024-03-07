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

namespace Datadog.Trace.Tests;

public class SpanMetaStructTests
{
    private const string MetaStructStr = "meta_struct";
    private const string FirstItemKey = "_dd.stack.exploit";
    private const string SecondItemKey = "secondValue";
    private const string SecondItemValue = "value";
    private static readonly IFormatterResolver FormatterResolver = SpanFormatterResolver.Instance;
    private static List<object> firstItemValue =
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

    [Fact]
    public void GivenAEncodedSpanWithMetaStruct_WhenDecoding_ThenMetaStructIsCorrectlyDecoded()
    {
        var span = new Span(new SpanContext(5, 6, samplingPriority: null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };

        // We add two elements to the meta struct
        span.MetaStruct.Add(FirstItemKey, firstItemValue);
        span.MetaStruct.Add(SecondItemKey, SecondItemValue);
        var spanBytes = new byte[] { };
        var spanBuffer = new SpanBuffer(10000, FormatterResolver);
        // We serialize the span
        var serializationResult = spanBuffer.TryWrite(new ArraySegment<Span>(new[] { span }), ref spanBytes);
        serializationResult.Should().Be(SpanBuffer.WriteStatus.Success);

        // Find the offset of the header "meta_struct" in the byte array
        int offset = FindMetaStructSection(spanBytes);
        offset.Should().BeGreaterThan(0);
        offset += MetaStructStr.Length;

        // Read the map header with 2 items
        var headerLength = MessagePackBinary.ReadMapHeader(spanBytes, offset, out var bytesRead);
        headerLength.Should().Be(2);
        offset += bytesRead;

        // We check every item in the map
        offset = CheckDictionaryItem(spanBytes, offset, FirstItemKey, firstItemValue);
        _ = CheckDictionaryItem(spanBytes, offset, SecondItemKey, SecondItemValue);
    }

    private static int CheckDictionaryItem(byte[] bytes, int offset, string expectedKey, object expectedValue)
    {
        // Read the key
        string result = MessagePackBinary.ReadString(bytes, offset, out var size);
        result.Should().Be(expectedKey);
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
}
