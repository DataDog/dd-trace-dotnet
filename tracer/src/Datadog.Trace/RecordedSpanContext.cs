// <copyright file="RecordedSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace;

/// <summary>
/// A span context that represents a span that will be recorded
/// </summary>
internal sealed class RecordedSpanContext(SpanContext context, string? operationName, string? resourceName)
    : MaybeRecordedSpanContext(context, operationName, resourceName)
{
}
