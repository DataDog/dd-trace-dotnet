// <copyright file="ScopeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;

internal static class ScopeHelper<TMarkerType>
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ActivatorHelper? ManualScopeActivator;
    // ReSharper disable once StaticMemberInGenericType
    private static readonly ActivatorHelper? ManualSpanContextActivator;

    static ScopeHelper()
    {
        var scopeType = typeof(TMarkerType).Assembly.GetType("Datadog.Trace.ManualScope");
        // Should never be null, but be safe.
        if (scopeType != null)
        {
            ManualScopeActivator = new ActivatorHelper(scopeType);
        }

        var spanType = typeof(TMarkerType).Assembly.GetType("Datadog.Trace.ManualSpanContext");
        // Should never be null, but be safe.
        if (spanType != null)
        {
            ManualSpanContextActivator = new ActivatorHelper(spanType);
        }
    }

    public static object? CreateManualScope(IScope scope)
    {
        if (ManualScopeActivator is null)
        {
            return null;
        }

        var manualScope = ManualScopeActivator.CreateInstance();
        manualScope
           .DuckCast<IManualScopeProxy>()
           .SetAutomatic(scope, scope.Span, scope.Span.Context);
        return manualScope;
    }

    public static object? CreateManualSpanContext(ISpanContext context)
    {
        if (ManualSpanContextActivator is null)
        {
            return null;
        }

        var manualSpan = ManualSpanContextActivator.CreateInstance();
        manualSpan
           .DuckCast<IManualSpanContextProxy>()
           .SetAutomatic(context);
        return manualSpan;
    }
}
