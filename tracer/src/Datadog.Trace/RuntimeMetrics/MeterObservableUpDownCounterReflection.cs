// <copyright file="MeterObservableUpDownCounterReflection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Resolves <c>Meter.CreateObservableUpDownCounter&lt;T&gt;(string, Func&lt;T&gt;, ...)</c> via reflection.
/// <c>Datadog.Trace</c> compiles against the net6.0 ref assembly (no UpDownCounter API), but on .NET 7+
/// host processes roll-forward provides <c>System.Diagnostics.DiagnosticSource</c> 7.0+ where the API
/// exists. On .NET 6 callers should fall back to <c>ObservableGauge</c>.
/// </summary>
internal static class MeterObservableUpDownCounterReflection
{
    // Pick the Func<T> overload (skip Func<Measurement<T>> and Func<IEnumerable<Measurement<T>>>).
    private static readonly MethodInfo? Method = typeof(Meter)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
        .FirstOrDefault(m =>
            m.Name == "CreateObservableUpDownCounter"
            && m.IsGenericMethodDefinition
            && m.GetParameters() is { Length: >= 2 } p
            && p[0].ParameterType == typeof(string)
            && p[1].ParameterType.IsGenericType
            && p[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<>)
            && p[1].ParameterType.GetGenericArguments()[0] == m.GetGenericArguments()[0]);

    public static bool TryRegister<T>(Meter meter, string name, Func<T> observeValue, string unit, string description)
        where T : struct
    {
        if (Method is null)
        {
            return false;
        }

        try
        {
            Method.MakeGenericMethod(typeof(T)).Invoke(meter, [name, observeValue, unit, description, null /* tags */]);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

#endif
