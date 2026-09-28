// <copyright file="FlagEvalEVPHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

internal sealed class FlagEvalEVPHook : Hook
{
    private readonly Func<bool> _canEnqueue;
    private readonly Action<string, string?, string?, string?, long, bool, string?, IReadOnlyDictionary<string, object?>?, int> _enqueue;
    private readonly Action _recordError;
    private readonly CaptureSnapshot _capture;

    internal FlagEvalEVPHook()
        : this(FeatureFlagsSdk.CanEnqueueEVP, FeatureFlagsSdk.EnqueueEVP, FeatureFlagsSdk.RecordEVPHookError, FlagEvaluationContextSnapshot.Capture)
    {
    }

    internal FlagEvalEVPHook(
        Func<bool> canEnqueue,
        Action<string, string?, string?, string?, long, bool, string?, IReadOnlyDictionary<string, object?>?, int> enqueue,
        Action recordError,
        CaptureSnapshot capture)
    {
        _canEnqueue = canEnqueue;
        _enqueue = enqueue;
        _recordError = recordError;
        _capture = capture;
    }

    internal delegate IReadOnlyDictionary<string, object?> CaptureSnapshot(EvaluationContext? context, out int omissionReasons);

    public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_canEnqueue())
            {
                return default;
            }

            var metadata = details.FlagMetadata;
            var consent = metadata?.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData) == true;
            var allocationKey = metadata?.GetString("__dd_allocation_key");
            var timestamp = metadata?.GetDouble(FeatureFlagMetadataKeys.EvaluationTimestampMs);
            // Missing metadata is possible when OpenFeature terminates before provider resolution.
            var evalTimeMs = timestamp is >= 0 and <= 253402300799999d && timestamp == Math.Truncate(timestamp.Value)
                                 ? (long)timestamp.Value
                                 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var errorCode = ToErrorCode(details.ErrorType);
            var targetingKey = context.EvaluationContext?.TargetingKey;
            IReadOnlyDictionary<string, object?>? attributes = null;
            var omissionReasons = 0;
            if (consent)
            {
                try
                {
                    attributes = _capture(context.EvaluationContext, out omissionReasons);
                }
                catch (Exception)
                {
                    // Preserve the observation, but discard context and never expose exception text.
                    omissionReasons = (int)ContextOmissionReason.SnapshotError;
                }
            }

            _enqueue(context.FlagKey, details.Variant, allocationKey, targetingKey, evalTimeMs, consent, errorCode, attributes, omissionReasons);
        }
        catch (Exception)
        {
            try
            {
                _recordError();
            }
            catch (Exception)
            {
                // Telemetry failures must not change the customer's evaluation.
            }
        }

        return default;
    }

    private static string? ToErrorCode(ErrorType error) => error switch
    {
        ErrorType.None => null,
        ErrorType.FlagNotFound => "FLAG_NOT_FOUND",
        ErrorType.InvalidContext => "INVALID_CONTEXT",
        ErrorType.ParseError => "PARSE_ERROR",
        ErrorType.ProviderFatal => "PROVIDER_FATAL",
        ErrorType.ProviderNotReady => "PROVIDER_NOT_READY",
        ErrorType.TargetingKeyMissing => "TARGETING_KEY_MISSING",
        ErrorType.TypeMismatch => "TYPE_MISMATCH",
        _ => "GENERAL",
    };
}
