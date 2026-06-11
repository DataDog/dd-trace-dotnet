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
    /// <c>TryGetHeader</c> is declared on the <c>MassTransit.Headers</c> interface but the concrete
    /// types (e.g. JsonTransportHeaders) implement it via *explicit interface implementation*
    /// — duck typing's default public-member binder cannot see those. The wildcard
    /// <c>ExplicitInterfaceTypeName = "*"</c> tells the binder to also match
    /// explicit impls (named <c>MassTransit.Headers.TryGetHeader</c> in IL).
    /// </summary>
    [Duck(ExplicitInterfaceTypeName = "*")]
    bool TryGetHeader(string key, out object value);
}
