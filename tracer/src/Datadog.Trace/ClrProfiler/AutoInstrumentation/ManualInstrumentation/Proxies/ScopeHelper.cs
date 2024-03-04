// <copyright file="ScopeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

internal static class ScopeHelper<TMarkerType>
{
    private static readonly Type IScopeType;
    private static readonly Type ISpanType;
    private static readonly Type ISpanContextType;

    static ScopeHelper()
    {
        var assembly = typeof(TMarkerType).Assembly;
        IScopeType = assembly.GetType("Datadog.Trace.IScope")!;
        ISpanType = assembly.GetType("Datadog.Trace.ISpan")!;
        ISpanContextType = assembly.GetType("Datadog.Trace.ISpanContext")!;
    }

    public static ManualScope CreateManualScope(IScope scope)
        => new(scope, CreateManualSpan(scope.Span), IScopeType);

    public static ManualSpan CreateManualSpan(ISpan span)
        => new(span, CreateManualSpanContext(span.Context), ISpanType);

    public static ManualSpanContext CreateManualSpanContext(ISpanContext context)
        => new(context, ISpanContextType);
}
