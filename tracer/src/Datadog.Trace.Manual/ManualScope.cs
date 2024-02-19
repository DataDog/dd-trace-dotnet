// <copyright file="ManualScope.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.DuckTyping;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Stubs;

namespace Datadog.Trace;

/// <summary>
/// Used to reverse duck-type an IScope in custom instrumentation
/// </summary>
internal class ManualScope : IScope
{
    private readonly ManualSpan _manualSpan = new();
    private IScope _proxy = NullScope.Instance;

    /// <summary>
    /// Gets the associated <see cref="IScope"/> from Datadog.Trace.
    /// Null when running under manual-only instrumentation.
    /// Non-null when running under automatic instrumentation
    /// </summary>
    internal object? AutomaticScope { get; private set; }

    public ISpan Span => _manualSpan;

    public void Dispose() => _proxy.Dispose();

    public void Close() => _proxy.Close();

    [DuckTypeTarget]
    internal void SetAutomatic(object scope, object span, object spanContext)
    {
        AutomaticScope = scope;
        _proxy = scope.DuckCast<IScope>() ?? NullScope.Instance;
        _manualSpan.SetAutomatic(span, spanContext);
    }
}
