// <copyright file="SpanContextMock.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Tagging;

namespace Datadog.Trace.Tests.Propagators;

internal class SpanContextMock
{
    public TraceId TraceId128 { get; set; }

    public ulong TraceId { get; set; }

    public ulong SpanId { get; set; }

    public string RawTraceId { get; set; }

    public string RawSpanId { get; set; }

    public string Origin { get; set; }

    public int? SamplingPriority { get; set; }

    public TraceTagCollection PropagatedTags { get; set; }

    public string AdditionalW3CTraceState { get; set; }

    public bool IsRemote { get; set; }

    public string LastParentId { get; set; }

    public ISpanContext Parent { get; set; }

    public ulong? ParentId { get; set; }

    public string ServiceName { get; set; }

    public TraceContext TraceContext { get; set; }
}
