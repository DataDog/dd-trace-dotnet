// <copyright file="OtelTagsEnumerationState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Tagging;

namespace Datadog.Trace.Activity.Helpers;

internal readonly struct OtelTagsEnumerationState(Span span)
{
    public Span Span { get; } = span;
}
