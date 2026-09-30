// <copyright file="OpenFeatureSdkInitializeAsyncIntegration.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;

/// <summary>
/// System.Threading.Tasks.Task Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk::InitializeAsync(System.Threading.CancellationToken) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.FeatureFlags.OpenFeature",
    TypeName = "Datadog.FeatureFlags.OpenFeature.FeatureFlagsSdk",
    MethodName = "InitializeAsync",
    ReturnTypeName = ClrNames.Task,
    ParameterTypeNames = [ClrNames.CancellationToken],
    MinimumVersion = "2.0.0",
    MaximumVersion = "2.*.*",
    IntegrationName = nameof(IntegrationId.OpenFeature))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class OpenFeatureSdkInitializeAsyncIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(ref CancellationToken cancellationToken)
        => new CallTargetState(scope: null, state: cancellationToken);

    internal static CallTargetReturn<Task> OnMethodEnd<TTarget>(Task returnValue, Exception? exception, in CallTargetState state)
    {
        if (exception is not null)
        {
            return new CallTargetReturn<Task>(returnValue);
        }

        // Agentless polling only starts here, because those requests go straight to Datadog and are
        // billable, so the tracer must not start them until application code adopts the provider. The
        // Remote Configuration source is already subscribed by then; this call waits for its first
        // configuration.
        if (TracerManager.Instance.FeatureFlags is { } featureFlags)
        {
            var cancellationToken = state.State is CancellationToken token ? token : default;
            return new CallTargetReturn<Task>(featureFlags.InitializeAsync(cancellationToken));
        }

        return new CallTargetReturn<Task>(returnValue);
    }
}
