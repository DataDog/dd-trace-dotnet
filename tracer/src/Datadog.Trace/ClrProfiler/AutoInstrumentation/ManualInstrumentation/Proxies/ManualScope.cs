// <copyright file="ManualScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

/// <summary>
/// Used to reverse duck-type an IScope in custom instrumentation
/// </summary>
internal class ManualScope
{
    private readonly ManualSpan _manualSpan;

    internal ManualScope(IScope scope, ManualSpan manualSpan, Type scopeType)
    {
        _manualSpan = manualSpan;
        AutomaticScope = scope;
        Proxy = this.DuckImplement(scopeType);
    }

    /// <summary>
    /// Gets the reverse-duck-type of this object for manual instrumentation
    /// </summary>
    internal object Proxy { get; }

    internal IScope AutomaticScope { get; }

    [DuckReverseMethod(ParameterTypeNames = new[] { "Datadog.Trace.IScope, Datadog.Trace.Manual" })]
    public object Span => _manualSpan.Proxy;

    [DuckReverseMethod]
    public void Dispose() => AutomaticScope.Dispose();

    [DuckReverseMethod]
    public void Close() => AutomaticScope.Close();
}
