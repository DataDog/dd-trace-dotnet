// <copyright file="MaybeRecordedSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace;

/// <summary>
/// A span context that represents a span that will not be recorded
/// </summary>
internal abstract class MaybeRecordedSpanContext(SpanContext context, string? operationName, string? resourceName)
{
    public SpanContext Context { get; } = context;

    public string? OperationName { get; } = operationName;

    public string? ResourceName { get; } = resourceName;
}
