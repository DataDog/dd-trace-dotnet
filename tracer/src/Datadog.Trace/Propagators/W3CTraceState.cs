// <copyright file="W3CTraceState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Propagators;

internal readonly struct W3CTraceState
{
    public readonly int? SamplingPriority;

    public readonly string? Origin;

    public readonly string? LastParent;

    // format is "_dd.p.key1:value1;_dd.p.key2:value2"
    public readonly string? PropagatedTags;

    // the string left in "tracestate" after removing "dd=*"
    public readonly string? AdditionalValues;

    public W3CTraceState(int? samplingPriority, string? origin, string? lastParent, string? propagatedTags, string? additionalValues)
    {
        SamplingPriority = samplingPriority;
        Origin = origin;
        LastParent = lastParent;
        PropagatedTags = propagatedTags;
        AdditionalValues = additionalValues;
    }
}
