// <copyright file="OtelTagsEnumerationState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Activity.Helpers;

/// <summary>
/// A state object for use with <see cref="AllocationFreeEnumerator{TEnumerable,TItem,TState}"/>.
/// We can add more properties to it if they are required by the enumerator, but it should
/// remain a readonly struct.
/// </summary>
internal readonly struct OtelTagsEnumerationState(Span span)
{
    public Span Span { get; } = span;
}
