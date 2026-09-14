// <copyright file="FeatureFlagsInitializeHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;

/// <summary>
/// Shared body for the two InitializeAsync calltargets, on the manual SDK and on the OpenFeature
/// provider. Both substitute the same real initialization for the same stub.
/// </summary>
internal static class FeatureFlagsInitializeHelper
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsInitializeHelper));

    // absorbDeliveryFailure is true on the manual surface only.
    // FeatureFlagsDeliveryUnavailableException is internal to Datadog.Trace, which
    // Datadog.Trace.Manual does not reference, so a fault there reaches the application as an
    // exception it cannot catch by type, and Evaluate() already reports PROVIDER_NOT_READY for the
    // same condition. The OpenFeature provider must keep faulting: the SDK turns that into an error
    // status and an error event.
    internal static CallTargetReturn<Task> OnMethodEnd(Task returnValue, Exception? exception, in CallTargetState state, bool absorbDeliveryFailure)
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
            var task = featureFlags.InitializeAsync(cancellationToken);
            return new CallTargetReturn<Task>(absorbDeliveryFailure ? ReportDeliveryFailureAsync(task) : task);
        }

        return new CallTargetReturn<Task>(returnValue);
    }

    private static async Task ReportDeliveryFailureAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (FeatureFlagsDeliveryUnavailableException ex)
        {
            Log.Warning(ex, "Feature Flags delivery could not start. Every evaluation returns its default value.");
        }
    }
}
