// <copyright file="W3CTraceParent.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators;

internal readonly struct W3CTraceParent
{
    public readonly TraceId TraceId;

    public readonly ulong ParentId;

    public readonly bool Sampled;

    public readonly string RawTraceId;

    public readonly string RawParentId;

    public W3CTraceParent(
        TraceId traceId,
        ulong parentId,
        bool sampled,
        string rawTraceId,
        string rawParentId)
    {
        TraceId = traceId;
        ParentId = parentId;
        Sampled = sampled;
        RawTraceId = rawTraceId;
        RawParentId = rawParentId;
    }
}
