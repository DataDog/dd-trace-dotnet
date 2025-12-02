// <copyright file="SamplingContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Tagging;

namespace Datadog.Trace.Sampling;

internal readonly struct SamplingContext
{
    public readonly SpanContext Context;
    public readonly string? OperationName;
    public readonly string? ResourceName;
    public readonly ITags? Tags;

    public SamplingContext(Span span)
    : this(span.Context, span.OperationName, span.ResourceName, span.Tags)
    {
    }

    private SamplingContext(SpanContext context, string? operationName, string? resourceName, ITags? tags)
    {
        Context = context;
        OperationName = operationName;
        ResourceName = resourceName;
        Tags = tags;
    }

    public static implicit operator SamplingContext(Span span) => new(span);
}
