// <copyright file="FlagEvalEVPHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    // DateTimeOffset.MaxValue expressed as whole Unix milliseconds.
    private const double MaxUnixTimeMilliseconds = 253402300799999d;

    private readonly Func<bool> _canEnqueue;
    private readonly Action<string, string?, string?, string?, long, string?, IReadOnlyDictionary<string, object?>?, int> _enqueue;
    private readonly Action _recordError;
    private readonly CaptureSnapshot _capture;
    // OpenFeature 2.3 passes the original context to Finally, not the context updated by Before.
    // Associate only the bounded capture with the result; weak keys also release it if OpenFeature
    // replaces that result after a hook exception. No customer context graph is retained.
    private readonly ConditionalWeakTable<ImmutableMetadata, EvaluatedContext> _evaluatedContexts = new();

    internal FlagEvalEVPHook()
        : this(FeatureFlagsSdk.CanEnqueueEVP, FeatureFlagsSdk.EnqueueEVP, FeatureFlagsSdk.RecordEVPHookError, FlagEvaluationContextSnapshot.Capture)
    {
    }

    internal FlagEvalEVPHook(
        Func<bool> canEnqueue,
        Action<string, string?, string?, string?, long, string?, IReadOnlyDictionary<string, object?>?, int> enqueue,
        Action recordError,
        CaptureSnapshot capture)
    {
        _canEnqueue = canEnqueue;
        _enqueue = enqueue;
        _recordError = recordError;
        _capture = capture;
    }

    internal delegate IReadOnlyDictionary<string, object?> CaptureSnapshot(EvaluationContext? context, out int omissionReasons);

    internal void CaptureEvaluation(EvaluationContext? context, ImmutableMetadata? metadata)
    {
        try
        {
            if (metadata is null || !_canEnqueue())
            {
                return;
            }

            IReadOnlyDictionary<string, object?>? attributes = null;
            var omissionReasons = 0;
            if (metadata.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData) == true)
            {
                try
                {
                    attributes = _capture(context, out omissionReasons);
                }
                catch (Exception)
                {
                    // Preserve the observation, but discard context and never expose exception text.
                    omissionReasons = (int)ContextOmissionReason.SnapshotError;
                }
            }

            _evaluatedContexts.Add(metadata, new EvaluatedContext(context?.TargetingKey, attributes, omissionReasons));
        }
        catch (Exception)
        {
            RecordError();
        }
    }

    public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var metadata = details.FlagMetadata;
            EvaluatedContext? evaluated = null;
            if (metadata is not null)
            {
                if (!_evaluatedContexts.TryGetValue(metadata, out evaluated))
                {
                    // Capture was rejected or failed. Never substitute the original hook context.
                    return default;
                }

                _evaluatedContexts.Remove(metadata);
            }

            if (!_canEnqueue())
            {
                return default;
            }

            var consent = metadata?.GetBool(FeatureFlagMetadataKeys.ObserveFullEvaluationData) == true;
            var allocationKey = metadata?.GetString("__dd_allocation_key");
            var timestamp = metadata?.GetDouble(FeatureFlagMetadataKeys.EvaluationTimestampMs);
            // Missing metadata is possible when OpenFeature terminates before provider resolution.
            var evalTimeMs = timestamp is >= 0 and <= MaxUnixTimeMilliseconds && timestamp == Math.Truncate(timestamp.Value)
                                 ? (long)timestamp.Value
                                 : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var errorCode = ToErrorCode(details.ErrorType);
            if (errorCode is null)
            {
                // Some evaluator failures carry descriptive messages that the provider does not
                // map to an enum. Use only their stable metadata code, never the message.
                errorCode = ToMetadataErrorCode(metadata?.GetString("errorCode"));
            }

            // Without result metadata, OpenFeature produced an SDK-level error. Consent defaults
            // to false, and the original invocation context is the only available context.
            var targetingKey = evaluated is null ? context.EvaluationContext?.TargetingKey : evaluated.TargetingKey;
            var attributes = evaluated?.Attributes;
            var omissionReasons = evaluated?.OmissionReasons ?? 0;
            var flags = omissionReasons | (consent ? FlagEvaluationBridge.ObserveFullEvaluationData : 0);
            // OpenFeature uses an empty variant for SDK-generated errors that return the caller's
            // default. Preserve genuine empty variants from successful or provider-attributed results.
            var variant = metadata is null && details.ErrorType != ErrorType.None && details.Variant == string.Empty
                              ? null
                              : details.Variant;
            _enqueue(context.FlagKey, variant, allocationKey, targetingKey, evalTimeMs, errorCode, attributes, flags);
        }
        catch (Exception)
        {
            RecordError();
        }

        return default;
    }

    // Keep this allowlist aligned with FlagEvaluationPrivacy.ErrorCodeForOutput in Datadog.Trace.
    // The provider and tracer validate independently across their assembly boundary.
    private static string? ToMetadataErrorCode(string? code) => code switch
    {
        null or "" => null,
        "FLAG_NOT_FOUND" or "INVALID_CONTEXT" or "PARSE_ERROR" or "PROVIDER_FATAL" or
        "PROVIDER_NOT_READY" or "TARGETING_KEY_MISSING" or "TYPE_MISMATCH" or "GENERAL" => code,
        _ => "GENERAL",
    };

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

    private void RecordError()
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

    private sealed class EvaluatedContext(string? targetingKey, IReadOnlyDictionary<string, object?>? attributes, int omissionReasons)
    {
        internal string? TargetingKey { get; } = targetingKey;

        internal IReadOnlyDictionary<string, object?>? Attributes { get; } = attributes;

        internal int OmissionReasons { get; } = omissionReasons;
    }
}
