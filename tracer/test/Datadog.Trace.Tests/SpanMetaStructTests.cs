// <copyright file="SpanMetaStructTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Agent.MessagePack;
using Datadog.Trace.Vendors.MessagePack;
using FluentAssertions;
using Xunit;

#nullable enable

namespace Datadog.Trace.Tests;

#pragma warning disable SA1649 // File name should match first type name
internal readonly struct StackFrame
#pragma warning restore SA1649 // File name should match first type name
{
    // "id": <unsigned integer: index of the stack frame(0 = top of stack)>,
    // "text": <string: raw stack frame> (optional),
    // "file": <string> (optional),
    // "line": <unsigned integer> (optional),
    // "column": <unsigned integer> (optional),
    // "namespace": <string> (optional),
    // "class_name": <string> (optional),
    // "function": <string> (optional),

    private readonly uint _id;
    private readonly string? _text;
    private readonly string? _file;
    private readonly uint? _line;
    private readonly uint? _column;
    private readonly string? _namespace;
    private readonly string? _className;
    private readonly string? _function;

    public StackFrame(uint id, string? text, string? file, uint? line, uint? column, string? ns, string? className, string? function)
    {
        _id = id;
        _text = text;
        _file = file;
        _line = line;
        _column = column;
        _namespace = ns;
        _className = className;
        _function = function;
    }

    public uint Id => _id;

    public string? Text => _text;

    public string? File => _file;

    public uint? Line => _line;

    public uint? Column => _column;

    public string? Namespace => _namespace;

    public string? ClassName => _className;

    public string? Function => _function;
}

internal readonly struct EventCategory
{
    // "type": EVENT_TYPE(optional),
    // "language": (php|nodejs|java|dotnet|go|python|ruby|cpp|...) (optional),
    // "id": <string: UUID of the stack trace> (optional),
    // "message": <string: generic message> (optional),
    // "frames": [STACK_FRAME]

    private readonly string? _type;
    private readonly string? _language;
    private readonly string? _id;
    private readonly string? _message;
    private readonly List<StackFrame> _frames;

    public EventCategory(string? type, string? language, string? id, string? message, List<StackFrame> frames)
    {
        _type = type;
        _language = language;
        _id = id;
        _message = message;
        _frames = frames;
    }

    public string? Type => _type;

    public string? Language => _language;

    public string? Id => _id;

    public string? Message => _message;

    public List<StackFrame> Frames => _frames;
}

public class SpanMetaStructTests
{
    [Fact]
    public void GivenAMetaStruct_WhenEncode_ThenDecodeIsOk0()
    {
        // var STACK_TRACES = "_dd.stack"
        var bytes = new byte[1];
        int offset = 0;

        Dictionary<string, object> metaStruct = new();
        metaStruct["_dd.stack.exploit.type"] = "type";
        metaStruct["_dd.stack.exploit.language"] = "dotnet";

        SpanMessagePackFormatter.EncodeMetaStruct(ref bytes, ref offset, metaStruct);
        offset.Should().BeGreaterThan(1);

        var readOffset = 0;
        var readOffsetTemp = 0;
        var headerBytes = MessagePackBinary.ReadString(bytes, readOffset, out readOffsetTemp);
        readOffset += readOffsetTemp;
        var header = MessagePackBinary.ReadMapHeader(bytes, readOffset, out readOffsetTemp);
        readOffset += readOffsetTemp;

        for (int i = 0; i < header; i++)
        {
            var key = MessagePackBinary.ReadString(bytes, readOffset, out readOffsetTemp);
            readOffset += readOffsetTemp;
            var value = MessagePackBinary.ReadString(bytes, readOffset, out readOffsetTemp);
            readOffset += readOffsetTemp;
            key.Should().BeOneOf("_dd.stack.exploit.type", "_dd.stack.exploit.language");
            value.Should().BeOneOf("type", "dotnet");
        }
    }

    [Fact]
    public void GivenAMetaStruct_WhenEncode_ThenDecodeIsOk()
    {
        // var STACK_TRACES = "_dd.stack"
        var bytes = new byte[1];
        int offset = 0;

        List<Dictionary<string, object>> frames = new List<Dictionary<string, object>>()
        {
            new Dictionary<string, object>
            {
                { "id", 0 },
                { "text", "text" },
                { "file", "file" },
                { "line", 1 },
                { "column", 2 },
                { "namespace", "namespace" },
            },
            new Dictionary<string, object>
            {
                { "id", 1 },
                { "text", "text2" },
                { "file", "file2" },
                { "line", 1 },
                { "column", 2 },
                { "namespace", "namespace" },
            }
        };

        Dictionary<string, object> vuln = new Dictionary<string, object>
        {
            { "type", "type" },
            { "language", "dotnet" },
            { "id", 1 },
            { "message", "message" },
            { "frames", frames }
        };

        List<object> exploit = new List<object>()
        {
            vuln
        };

        Dictionary<string, object> metaStruct = new();
        metaStruct["exploit"] = exploit;

        SpanMessagePackFormatter.EncodeMetaStruct(ref bytes, ref offset, metaStruct);

        offset.Should().BeGreaterThan(1);
    }
}
