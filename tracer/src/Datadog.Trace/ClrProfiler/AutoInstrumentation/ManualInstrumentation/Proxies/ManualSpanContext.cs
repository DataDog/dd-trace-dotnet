// <copyright file="ManualSpanContext.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Used to reverse duck-type an ISpanContext in custom instrumentation
/// </summary>
internal class ManualSpanContext : ISpanContext
{
    public ManualSpanContext(ISpanContext context, Type spanContextType)
    {
        AutomaticContext = context;
        Proxy = this.DuckImplement(spanContextType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    internal ISpanContext AutomaticContext { get; }

    [DuckReverseMethod]
    public ulong TraceId => AutomaticContext.TraceId;

    [DuckReverseMethod]
    public ulong SpanId => AutomaticContext.SpanId;

    [DuckReverseMethod]
    public string? ServiceName => AutomaticContext.ServiceName;
}
