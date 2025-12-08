// <copyright file="IntegrationOptions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers;

internal static class IntegrationOptions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RestoreScopeFromAsyncExecution(in CallTargetState state)
    {
        // Restore previous scope and the previous DistributedTrace if there is a continuation
        // This is used to mimic the ExecutionContext copy from the StateMachine
        if (Tracer.Instance.ScopeManager is IScopeRawAccess rawAccess)
        {
            rawAccess.Active = state.PreviousScope;
            DistributedTracer.Instance.SetSpanContext(state.PreviousDistributedSpanContext);
        }
    }
}

#pragma warning disable SA1402
internal static class IntegrationOptions<TIntegration, TTarget>
#pragma warning restore SA1402
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntegrationOptions<TIntegration, TTarget>));

    private static readonly Lazy<IntegrationId?> _integrationId = new(() => InstrumentationDefinitions.GetIntegrationId(typeof(TIntegration).FullName, typeof(TTarget)));
    private static volatile bool _disableIntegration = false;

    internal static bool IsIntegrationEnabled => !_disableIntegration;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DisableIntegration() => _disableIntegration = true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void LogException(Exception exception)
    {
        // ReSharper disable twice ExplicitCallerInfoArgument
        Log.Error(exception, "Exception occurred when calling the CallTarget integration continuation.");
        if (exception is DuckTypeException or TargetInvocationException { InnerException: DuckTypeException })
        {
            Log.Warning("DuckTypeException has been detected, the integration <{TIntegration}, {TTarget}> will be disabled.", typeof(TIntegration), typeof(TTarget));
            if (_integrationId.Value is { } integrationId)
            {
                TelemetryFactory.Metrics.RecordCountSharedIntegrationsError(integrationId.GetMetricTag(), MetricTags.InstrumentationError.DuckTyping);
                Tracer.Instance.TracerManager.Telemetry.IntegrationDisabledDueToError(integrationId, nameof(DuckTypeException));
            }

            _disableIntegration = true;
        }
        else if (exception is CallTargetInvokerException)
        {
            Log.Warning("CallTargetInvokerException has been detected, the integration <{TIntegration}, {TTarget}> will be disabled.", typeof(TIntegration), typeof(TTarget));
            if (_integrationId.Value is { } integrationId)
            {
                TelemetryFactory.Metrics.RecordCountSharedIntegrationsError(integrationId.GetMetricTag(), MetricTags.InstrumentationError.Invoker);
                Tracer.Instance.TracerManager.Telemetry.IntegrationDisabledDueToError(integrationId, nameof(CallTargetInvokerException));
            }

            _disableIntegration = true;
        }
        else if (exception is MissingMemberException or TargetInvocationException { InnerException: MissingMemberException })
        {
            Log.Warning(
                "MissingMemberException has been detected, the integration <{TIntegration}, {TTarget}> will be disabled. "
              + "This may be because the application uses trimming. "
              + "To make your app compatible with trimming, add a reference to either the Datadog.Trace or "
              + "Datadog.Trace.Trimming NuGet package. If the problem persists, or you are not using trimming, please contact support.",
                typeof(TIntegration),
                typeof(TTarget));
            if (_integrationId.Value is { } integrationId)
            {
                TelemetryFactory.Metrics.RecordCountSharedIntegrationsError(integrationId.GetMetricTag(), MetricTags.InstrumentationError.MissingMember);
                Tracer.Instance.TracerManager.Telemetry.IntegrationDisabledDueToError(integrationId, nameof(MissingMemberException));
            }

            _disableIntegration = true;
        }
        else
        {
            if (_integrationId.Value is { } integrationId)
            {
                TelemetryFactory.Metrics.RecordCountSharedIntegrationsError(integrationId.GetMetricTag(), MetricTags.InstrumentationError.Execution);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordTelemetry()
    {
        if (_integrationId.Value is { } integrationIdValue)
        {
            Tracer.Instance.TracerManager.Telemetry.IntegrationRunning(integrationIdValue);
        }
    }
}
