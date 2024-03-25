// <copyright file="SpanMetaStructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.AppSec.Rasp;
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
    private static StackFrame stackFrame1 = new StackFrame(1, "text", "file.cs", 33, null, "testnamespace", "class1", "method1");
    private static StackFrame stackFrame2 = new StackFrame(2, "text2", "file2.cs", 55, null, "testnamespace", "class2", "method2");
    private static StackFrame stackFrame3 = new StackFrame(3, "text3", "file3.cs", 77, null, "testnamespace", "class3", "method3");
    private static StackFrame stackFrame4 = new StackFrame(4, "text4", "file4.cs", 77, null, "testnamespace", "class4", "method4");

    private static List<object> stackData =
                new List<object>()
                {
                    MetaStructHelper.StackToDictionary("typevalue", "dotnet", "213123213123213", null, new List<StackFrame>() { stackFrame1, stackFrame2 }),
                    MetaStructHelper.StackToDictionary("type2222", "dotnet", "test55555", null, new List<StackFrame>() { stackFrame3, stackFrame4 }),
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
            span.SetMetaStruct(item.Item1, MetaStructHelper.ObjectToByteArray(item.Item2));
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
}
