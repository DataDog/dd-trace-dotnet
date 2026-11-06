// <copyright file="ExposureHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// An OpenFeature hook that hands each completed flag evaluation to a callback, with the Datadog
/// exposure decision, so an application can record exposures in its own data store.
/// <para>
/// The provider also puts the exposure data in the flag metadata, so any OpenFeature hook can read
/// it; this hook reads it for you and uses the final exposure decision. Register it with
/// <c>OpenFeature.Api.Instance.AddHooks</c> or on a client. The callback runs for every evaluation,
/// including errors and repeats; use <see cref="ExposureEvaluation.ShouldSend"/> to keep the new
/// or changed exposures. When the provider is registered directly with OpenFeature and evaluated
/// through a client, these are exactly the exposures it sends to Datadog while
/// <c>DD_FEATURE_FLAGS_EXPOSURES_DATADOG_LOGGING_ENABLED</c> is on, and the same decision with it
/// off. Inside OpenFeature's <c>MultiProvider</c>, the exposure is recorded before the client's
/// <c>after</c> hooks run, so an evaluation that one of them fails can still have been sent.
/// </para>
/// </summary>
public sealed class ExposureHook : Hook
{
    private readonly Action<ExposureEvaluation> _onEvaluation;
    private readonly ILogger? _logger;
    private int _callbackFailed;

    /// <summary> Initializes a new instance of the <see cref="ExposureHook"/> class. </summary>
    /// <param name="onEvaluation">Called for every completed evaluation, on the evaluation thread.</param>
    /// <param name="logger">
    /// Receives a warning for the first callback exception and debug entries for later ones. Without
    /// a logger, only the first one is written, to <see cref="System.Diagnostics.Trace"/>. They never
    /// affect the evaluation.
    /// </param>
    public ExposureHook(Action<ExposureEvaluation> onEvaluation, ILogger? logger = null)
    {
        _onEvaluation = onEvaluation ?? throw new ArgumentNullException(nameof(onEvaluation));
        _logger = logger;
        FeatureFlagsSdk.OnExposureHookCreated();
    }

    /// <inheritdoc/>
    public override ValueTask FinallyAsync<T>(
        HookContext<T> context,
        FlagEvaluationDetails<T> details,
        IReadOnlyDictionary<string, object>? hints = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var evaluationContext = FeatureFlagsSdk.GetEvaluatedContext(details.FlagMetadata) ?? context.EvaluationContext;
            _onEvaluation(
                new ExposureEvaluation(
                    details.ErrorType == ErrorType.None,
                    details.FlagKey,
                    details.Variant,
                    details.FlagMetadata?.GetString(FeatureFlagMetadataKeys.AllocationKey),
                    evaluationContext?.TargetingKey,
                    evaluationContext,
                    details.FlagMetadata));
        }
        catch (Exception ex)
        {
            // The callback is application code; it must never affect the evaluation.
            if (Interlocked.Exchange(ref _callbackFailed, 1) == 0)
            {
                if (_logger is not null)
                {
                    _logger.LogWarning(ex, "Exposure callback failed; later failures are logged at debug level");
                }
                else
                {
                    System.Diagnostics.Trace.TraceWarning($"Exposure callback failed; later failures are not logged without an ILogger: {ex}");
                }
            }
            else
            {
                // Without a logger, later failures are not written: formatting each exception on the
                // evaluation thread would cost every evaluation of a callback that keeps failing.
                _logger?.LogDebug(ex, "Exposure callback failed");
            }
        }

        return default;
    }
}
