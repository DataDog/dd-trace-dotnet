// <copyright file="SpanMetaStructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
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
    private const string StackStr = "_dd.stack.exploit";
    private string stack2 = "value";
    private List<object> stack =
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
    public void GivenAMetaStruct_WhenEncode_ThenDecodeIsOk()
    {
        var span = new Span(new SpanContext(5, 6, samplingPriority: null, serviceName: "service-name"), DateTimeOffset.Now) { OperationName = "operation-name" };
        span.MetaStruct.Add(StackStr, stack);
        span.MetaStruct.Add("secondValue", stack2);

        var bytes = new byte[] { };
        // SpanMessagePackFormatter.Instance.Serialize(bytes, 0, );

        var formatterResolver = SpanFormatterResolver.Instance;
        var spanBuffer = new SpanBuffer(10000, formatterResolver);
        spanBuffer.TryWrite(new ArraySegment<Span>(new[] { span }), ref bytes).Should().Be(SpanBuffer.WriteStatus.Success);

        // Decode the bytes
        // Find the offset of the header "meta_struct" in the buffer
        int offset = FindMetaStructSection(bytes);
        offset.Should().NotBe(0);
        offset += MetaStructStr.Length;
        var headerLength = MessagePackBinary.ReadMapHeader(bytes, offset, out var size);
        headerLength.Should().Be(2);
        offset += size;
        var result = MessagePackBinary.ReadString(bytes, offset, out size);
        result.Should().Be(StackStr);
        offset += size;
        var newArray = MessagePackBinary.ReadBytes(bytes, offset, out size);
        var newResult = PrimitiveObjectFormatter.Instance.Deserialize(newArray, 0, formatterResolver, out var readSize);
        // We should read the whole array
        readSize.Should().Be(newArray.Length);
        newResult.Should().BeOfType<object[]>();
        (newResult as object[])![0].Should().BeEquivalentTo(stack[0]);
        (newResult as object[])![1].Should().BeEquivalentTo(stack[1]);

        // We read the second object of the dictionary
        offset += size;
        result = MessagePackBinary.ReadString(bytes, offset, out size);
        result.Should().Be("secondValue");
        offset += size;
        newArray = MessagePackBinary.ReadBytes(bytes, offset, out size);
        newResult = PrimitiveObjectFormatter.Instance.Deserialize(newArray, 0, formatterResolver, out readSize);
        // We should read the whole array
        readSize.Should().Be(newArray.Length);
        newResult.Should().BeOfType<string>();
        (newResult as string)!.Should().Be(stack2);
    }

    private static int FindMetaStructSection(byte[] bytes)
    {
        var offset = 0;
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
