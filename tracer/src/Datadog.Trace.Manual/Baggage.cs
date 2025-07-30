// <copyright file="Baggage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections;
using System.Runtime.CompilerServices;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

/// <summary>
/// Baggage is a collection of name-value pairs that are propagated to downstream services.
/// </summary>
public static class Baggage
{
    // only used when IL-rewriting is not available
    private static IDictionary<string, string?>? _current;

    /// <summary>
    /// Gets or sets the baggage collection for the current execution context.
    /// </summary>
    [Instrumented]
    public static IDictionary<string, string?> Current
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        get
        {
            // auto-instrumentation will return Trace.Baggage.Current instead
            return _current ??= new Dictionary<string, string?>();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        set
        {
            // auto-instrumentation will add:
            // Trace.Baggage.Current = value switch
            // {
            //     Trace.Baggage b => b,
            //     null => new Trace.Baggage(),
            //     _ => new Trace.Baggage(value)
            // };
            _current = value;
        }
    }
}
