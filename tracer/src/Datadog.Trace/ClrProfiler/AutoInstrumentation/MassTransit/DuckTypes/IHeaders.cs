// <copyright file="IHeaders.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.DuckTypes;

/// <summary>
/// Minimal duck-typing interface for MassTransit headers collection.
/// Supports extracting trace context from TransportHeaders or Headers.
/// </summary>
internal interface IHeaders
{
    /// <summary>
    /// Looks up a single header by key. Backed by a dictionary in MassTransit's
    /// implementations, so this is O(1) and avoids the per-call allocations from GetAll().
    /// <para/>
    /// In MassTransit 7.x, <c>JsonTransportHeaders</c> implements <c>TryGetHeader</c> as an
    /// explicit interface implementation of <c>MassTransit.Headers</c>; the IL method name is
    /// <c>MassTransit.Headers.TryGetHeader</c> (private, not visible to the default public binder).
    /// <c>ExplicitInterfaceTypeName = "MassTransit.Headers"</c> directs the binder to that exact
    /// explicit implementation.
    /// </summary>
    [Duck(ExplicitInterfaceTypeName = "MassTransit.Headers")]
    bool TryGetHeader(string key, out object value);
}
