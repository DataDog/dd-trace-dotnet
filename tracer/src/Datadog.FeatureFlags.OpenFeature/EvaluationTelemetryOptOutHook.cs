// <copyright file="EvaluationTelemetryOptOutHook.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

// One wrapper belongs to one evaluation. OpenFeature versions before 2.5.0 pass the final merged context to
// provider Before hooks, but passes the original context to Finally. Never share this decision
// between evaluations, even when their immutable contexts or provider are shared.
internal sealed class EvaluationTelemetryOptOutHook(Hook inner) : Hook
{
    private readonly Hook _inner = inner;
    private bool? _optedOut;

    public override ValueTask<EvaluationContext> BeforeAsync<T>(HookContext<T> context, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        _optedOut = IsOptedOut(context.EvaluationContext);
        return _inner.BeforeAsync(context, hints, cancellationToken);
    }

    public override ValueTask AfterAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        => _inner.AfterAsync(context, details, hints, cancellationToken);

    public override ValueTask ErrorAsync<T>(HookContext<T> context, Exception error, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
        => _inner.ErrorAsync(context, error, hints, cancellationToken);

    public override ValueTask FinallyAsync<T>(HookContext<T> context, FlagEvaluationDetails<T> details, IReadOnlyDictionary<string, object>? hints = null, CancellationToken cancellationToken = default)
    {
        // Sync calls skip Before. An application Before failure can also prevent this wrapper's
        // Before from running. Both paths use the immutable context supplied to Finally.
        return (_optedOut ?? IsOptedOut(context.EvaluationContext))
            ? default
            : _inner.FinallyAsync(context, details, hints, cancellationToken);
    }

    private static bool IsOptedOut(EvaluationContext? context)
        => context is not null
        && context.TryGetValue("dd_evaluation_telemetry_opt_out", out var value)
        && value is { IsBoolean: true, AsBoolean: true };
}
