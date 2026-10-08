// <copyright file="CallTargetAotRegistry.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

/// <summary>
/// Helpers for the registrations <c>dd-trace aot instrument</c> generates in the instrumented assemblies, which fill
/// <see cref="CallTargetAot{TIntegration, TDelegate}"/> from the generic context of each instrumented method.
/// </summary>
internal static class CallTargetAotRegistry
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CallTargetAotRegistry));

    /// <summary>
    /// Same check as <see cref="IntegrationMapper"/> for an integration parameter of a concrete type: the instantiation
    /// is only known at runtime when the target is generic.
    /// </summary>
    internal static bool CanAssign(Type targetType, Type sourceType)
        => targetType.IsAssignableFrom(sourceType) || (sourceType.IsEnum && targetType.IsEnum);

    internal static bool IsSameType(Type left, Type right) => left == right;

    /// <summary>
    /// Called by a registration that failed unexpectedly. Its handlers stay unregistered and fall back to
    /// <see cref="IntegrationMapper"/>, which fails in NativeAOT and disables the integration.
    /// </summary>
    internal static void LogRegistrationError(Exception exception)
        => Log.Error(exception, "The NativeAOT CallTarget registration of an instrumented method failed.");
}
