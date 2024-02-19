// <copyright file="ManualSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Stubs;

namespace Datadog.Trace;

/// <summary>
/// Used to reverse duck-type an ISpanContext in custom instrumentation
/// </summary>
internal class ManualSpanContext : ISpanContext
{
    private ISpanContext _proxy = NullSpanContext.Instance;

    /// <summary>
    /// Gets the associated <see cref="ISpanContext"/> from Datadog.Trace.
    /// Null when running under manual-only instrumentation.
    /// Non-null when running under automatic instrumentation
    /// </summary>
    internal object? AutomaticContext { get; private set; }

    public ulong TraceId => _proxy.TraceId;

    public ulong SpanId => _proxy.SpanId;

    public string? ServiceName => _proxy.ServiceName;

    [DuckTypeTarget]
    internal void SetAutomatic(object context)
    {
        AutomaticContext = context;
        _proxy = context.DuckCast<ISpanContext>() ?? NullSpanContext.Instance;
    }
}
