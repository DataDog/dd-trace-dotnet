// <copyright file="MeterObservableUpDownCounterReflection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;

namespace Datadog.Trace.RuntimeMetrics;

/// <summary>
/// Resolves <c>Meter.CreateObservableUpDownCounter</c> overloads via reflection.
/// <c>Datadog.Trace</c> compiles against the net6.0 ref assembly (no UpDownCounter API), but on
/// .NET 7+ host processes roll-forward provides <c>System.Diagnostics.DiagnosticSource</c> 7.0+
/// where the API exists. On .NET 6 hosts callers should fall back to <c>ObservableGauge</c>.
/// </summary>
internal static class MeterObservableUpDownCounterReflection
{
    // Pin to the 4-parameter overloads (string name, Func<...>, string? unit, string? description).
    // .NET 7's DiagnosticSource 7.0 only ships the 4-param shape; .NET 8+ adds 5-param overloads with
    // a required `tags` argument alongside the original 4-param ones. Pinning to Length == 4 picks
    // an overload that exists on every supported host and avoids ambiguity on .NET 8+.
    private static readonly MethodInfo? FuncOfTMethod = FindOverload(secondParam =>
        secondParam.IsGenericType
        && secondParam.GetGenericTypeDefinition() == typeof(Func<>)
        && secondParam.GetGenericArguments()[0].IsGenericMethodParameter);

    private static readonly MethodInfo? FuncOfMeasurementsMethod = FindOverload(secondParam =>
    {
        if (!secondParam.IsGenericType || secondParam.GetGenericTypeDefinition() != typeof(Func<>))
        {
            return false;
        }

        var inner = secondParam.GetGenericArguments()[0];
        if (!inner.IsGenericType || inner.GetGenericTypeDefinition() != typeof(IEnumerable<>))
        {
            return false;
        }

        var measurement = inner.GetGenericArguments()[0];
        return measurement.IsGenericType && measurement.GetGenericTypeDefinition() == typeof(Measurement<>);
    });

    public static bool TryRegister<T>(Meter meter, string name, Func<T> observeValue, string unit, string description)
        where T : struct
        => Invoke(FuncOfTMethod, meter, typeof(T), name, observeValue, unit, description);

    public static bool TryRegisterMulti<T>(Meter meter, string name, Func<IEnumerable<Measurement<T>>> observeValues, string unit, string description)
        where T : struct
        => Invoke(FuncOfMeasurementsMethod, meter, typeof(T), name, observeValues, unit, description);

    private static bool Invoke(MethodInfo? method, Meter meter, Type genericArg, string name, object observe, string unit, string description)
    {
        if (method is null)
        {
            return false;
        }

        method.MakeGenericMethod(genericArg).Invoke(meter, [name, observe, unit, description]);
        return true;
    }

    private static MethodInfo? FindOverload(Func<Type, bool> matchesSecondParam) =>
        typeof(Meter).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m =>
                m.Name == "CreateObservableUpDownCounter"
                && m.IsGenericMethodDefinition
                && m.GetParameters() is { Length: 4 } p
                && p[0].ParameterType == typeof(string)
                && matchesSecondParam(p[1].ParameterType));
}

#endif
