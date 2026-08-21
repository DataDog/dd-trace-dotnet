// <copyright file="FeatureFlagsSdkInitializeAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Datadog_Trace_Manual;

/// <summary>
/// System.Threading.Tasks.Task Datadog.Trace.FeatureFlags.FeatureFlagsSdk::InitializeAsync(System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.FeatureFlags.FeatureFlagsSdk",
    MethodName = "InitializeAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.CancellationToken],
    MinimumVersion = "3.31.0",
    MaximumVersion = "3.*.*",
    IntegrationName = nameof(IntegrationId.DatadogTraceManual))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class FeatureFlagsSdkInitializeAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref CancellationToken cancellationToken)
        => new CallTargetState(scope: null, state: cancellationToken);

    internal static CallTargetReturn<Task> OnMethodEnd<TTarget>(Task returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            return new CallTargetReturn<Task>(returnValue);
        }

        // Delivery only starts here, because requesting configuration is billable, and the tracer
        // must not start until application code adopts the provider.
        if (TracerManager.Instance.FeatureFlags is { } featureFlags)
        {
            var cancellationToken = state.State is CancellationToken token ? token : default;
            return new CallTargetReturn<Task>(featureFlags.InitializeAsync(cancellationToken));
        }

        return new CallTargetReturn<Task>(returnValue);
    }
}
