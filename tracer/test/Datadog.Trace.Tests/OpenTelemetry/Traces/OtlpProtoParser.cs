// <copyright file="OtlpProtoParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Google.Protobuf;

#nullable enable

namespace Datadog.Trace.Tests.OpenTelemetry.Traces;

/// <summary>
/// Minimal OTLP protobuf decoder used to verify the output of
/// <see cref="Datadog.Trace.OpenTelemetry.Traces.OtlpTracesProtobufSerializer"/>.
/// </summary>
/// <remarks>
/// Intentionally avoids generated .proto bindings: we keep the test surface tiny by
/// walking the wire format directly via <see cref="Google.Protobuf.CodedInputStream"/>
/// and <see cref="Google.Protobuf.WireFormat"/>. This sidesteps the need to vendor or
/// generate `opentelemetry.proto.*` C# types in the test project and keeps drift in this
/// parser explicit (one switch arm per OTLP field) rather than hidden in generated code.
/// </remarks>
internal static class OtlpProtoParser
{
    public static ExportTraceServiceRequest ParseExportTraceServiceRequest(byte[] buffer, int offset, int length)
    {
        var slice = new byte[length];
        Buffer.BlockCopy(buffer, offset, slice, 0, length);
        return ParseExportTraceServiceRequest(slice);
    }

    private static ExportTraceServiceRequest ParseExportTraceServiceRequest(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var request = new ExportTraceServiceRequest();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1: // resource_spans
                    request.ResourceSpans.Add(ParseResourceSpans(input.ReadBytes().ToByteArray()));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return request;
    }

    private static ResourceSpans ParseResourceSpans(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var resourceSpans = new ResourceSpans();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1: // resource
                    resourceSpans.Resource = ParseResource(input.ReadBytes().ToByteArray());
                    break;
                case 2: // scope_spans
                    resourceSpans.ScopeSpans.Add(ParseScopeSpans(input.ReadBytes().ToByteArray()));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return resourceSpans;
    }

    private static Resource ParseResource(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var resource = new Resource();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1: // attributes
                    resource.Attributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return resource;
    }

    private static ScopeSpans ParseScopeSpans(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var scopeSpans = new ScopeSpans();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 2: // spans
                    scopeSpans.Spans.Add(ParseSpan(input.ReadBytes().ToByteArray()));
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return scopeSpans;
    }

    private static Span ParseSpan(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var span = new Span();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    span.TraceId = input.ReadBytes().ToByteArray();
                    break;
                case 2:
                    span.SpanId = input.ReadBytes().ToByteArray();
                    break;
                case 4:
                    span.ParentSpanId = input.ReadBytes().ToByteArray();
                    break;
                case 5:
                    span.Name = input.ReadString();
                    break;
                case 6:
                    span.Kind = input.ReadInt32();
                    break;
                case 7:
                    span.StartTimeUnixNano = input.ReadFixed64();
                    break;
                case 8:
                    span.EndTimeUnixNano = input.ReadFixed64();
                    break;
                case 9:
                    span.Attributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                    break;
                case 10:
                    span.DroppedAttributesCount = input.ReadUInt64();
                    break;
                case 11:
                    span.Events.Add(ParseEvent(input.ReadBytes().ToByteArray()));
                    break;
                case 12:
                    span.DroppedEventsCount = input.ReadUInt64();
                    break;
                case 13:
                    span.Links.Add(ParseLink(input.ReadBytes().ToByteArray()));
                    break;
                case 14:
                    span.DroppedLinksCount = input.ReadUInt64();
                    break;
                case 15:
                    span.Status = ParseStatus(input.ReadBytes().ToByteArray());
                    break;
                case 16:
                    span.Flags = input.ReadFixed32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return span;
    }

    private static Event ParseEvent(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var evt = new Event();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    evt.TimeUnixNano = input.ReadFixed64();
                    break;
                case 2:
                    evt.Name = input.ReadString();
                    break;
                case 3:
                    evt.Attributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                    break;
                case 4:
                    evt.DroppedAttributesCount = input.ReadUInt64();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return evt;
    }

    private static Link ParseLink(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var link = new Link();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    link.TraceId = input.ReadBytes().ToByteArray();
                    break;
                case 2:
                    link.SpanId = input.ReadBytes().ToByteArray();
                    break;
                case 3:
                    link.TraceState = input.ReadString();
                    break;
                case 4:
                    link.Attributes.Add(ParseKeyValue(input.ReadBytes().ToByteArray()));
                    break;
                case 5:
                    link.DroppedAttributesCount = input.ReadUInt64();
                    break;
                case 6:
                    link.Flags = input.ReadFixed32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return link;
    }

    private static Status ParseStatus(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var status = new Status();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 2:
                    status.Message = input.ReadString();
                    break;
                case 3:
                    status.Code = input.ReadInt32();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return status;
    }

    private static KeyValue ParseKeyValue(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var kv = new KeyValue();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    kv.Key = input.ReadString();
                    break;
                case 2:
                    kv.Value = ParseAnyValue(input.ReadBytes().ToByteArray());
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return kv;
    }

    private static AnyValue ParseAnyValue(byte[] buffer)
    {
        var input = new CodedInputStream(buffer);
        var value = new AnyValue();
        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            switch (WireFormat.GetTagFieldNumber(tag))
            {
                case 1:
                    value.StringValue = input.ReadString();
                    break;
                case 2:
                    value.BoolValue = input.ReadBool();
                    break;
                case 3:
                    value.IntValue = input.ReadInt64();
                    break;
                case 4:
                    value.DoubleValue = input.ReadDouble();
                    break;
                case 7:
                    value.BytesValue = input.ReadBytes().ToByteArray();
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }

        return value;
    }

#pragma warning disable SA1402 // File may only contain a single type
    internal sealed class ExportTraceServiceRequest
    {
        public List<ResourceSpans> ResourceSpans { get; } = new();
    }

    internal sealed class ResourceSpans
    {
        public Resource? Resource { get; set; }

        public List<ScopeSpans> ScopeSpans { get; } = new();
    }

    internal sealed class Resource
    {
        public List<KeyValue> Attributes { get; } = new();
    }

    internal sealed class ScopeSpans
    {
        public List<Span> Spans { get; } = new();
    }

    internal sealed class Span
    {
        public byte[] TraceId { get; set; } = Array.Empty<byte>();

        public byte[] SpanId { get; set; } = Array.Empty<byte>();

        public byte[] ParentSpanId { get; set; } = Array.Empty<byte>();

        public string Name { get; set; } = string.Empty;

        public int Kind { get; set; }

        public ulong StartTimeUnixNano { get; set; }

        public ulong EndTimeUnixNano { get; set; }

        public List<KeyValue> Attributes { get; } = new();

        public ulong DroppedAttributesCount { get; set; }

        public List<Event> Events { get; } = new();

        public ulong DroppedEventsCount { get; set; }

        public List<Link> Links { get; } = new();

        public ulong DroppedLinksCount { get; set; }

        public Status? Status { get; set; }

        public uint Flags { get; set; }
    }

    internal sealed class Event
    {
        public ulong TimeUnixNano { get; set; }

        public string Name { get; set; } = string.Empty;

        public List<KeyValue> Attributes { get; } = new();

        public ulong DroppedAttributesCount { get; set; }
    }

    internal sealed class Link
    {
        public byte[] TraceId { get; set; } = Array.Empty<byte>();

        public byte[] SpanId { get; set; } = Array.Empty<byte>();

        public string TraceState { get; set; } = string.Empty;

        public List<KeyValue> Attributes { get; } = new();

        public ulong DroppedAttributesCount { get; set; }

        public uint Flags { get; set; }
    }

    internal sealed class Status
    {
        public string Message { get; set; } = string.Empty;

        public int Code { get; set; }
    }

    internal sealed class KeyValue
    {
        public string Key { get; set; } = string.Empty;

        public AnyValue Value { get; set; } = new();
    }

    internal sealed class AnyValue
    {
        public string? StringValue { get; set; }

        public bool? BoolValue { get; set; }

        public long? IntValue { get; set; }

        public double? DoubleValue { get; set; }

        public byte[]? BytesValue { get; set; }
    }
#pragma warning restore SA1402
}
