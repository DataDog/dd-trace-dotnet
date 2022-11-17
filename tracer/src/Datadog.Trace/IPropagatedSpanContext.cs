// <copyright file="IPropagatedSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace;

internal interface IPropagatedSpanContext : ISpanContext
{
    string? RawTraceId { get; }

    string? RawSpanId { get; }

    int? SamplingPriority { get; }

    string? Origin { get; }

    string? PropagatedTags { get; }
}
