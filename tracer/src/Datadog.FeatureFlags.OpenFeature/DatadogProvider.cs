// <copyright file="DatadogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// OpenFeature V2.0.0+ Provider for Datadog
/// </summary>
public sealed class DatadogProvider : global::OpenFeature.FeatureProvider, IDisposable
{
    private static Action? _onNewConfig = null;
    private readonly Metadata _metadata = new Metadata("datadog-openfeature-provider");
#if NET6_0_OR_GREATER
    private readonly FlagEvalMetricsHook _metricsHook;

    // EVP flag evaluation hook — registered alongside the OTel FlagEvalMetricsHook unless
    // the killswitch DD_FLAGGING_EVALUATION_COUNTS_ENABLED is set to "false".
    // Routes through FeatureFlagsSdk.EnqueueEVP (static delegate bridge wired by FeatureFlagsModule).
    private readonly FlagEvalEVPHook? _evpHook;
#endif

    /// <summary> Initializes a new instance of the <see cref="DatadogProvider"/> class. </summary>
    public DatadogProvider()
    {
        FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => SignalGeneralUpdate());
#if NET6_0_OR_GREATER
        _metricsHook = new FlagEvalMetricsHook();

        // Register the EVP hook unless the killswitch is set to "false".
        // Default is enabled (DD_FLAGGING_EVALUATION_COUNTS_ENABLED absent or any value != "false").
        // The EVP hook itself routes through FeatureFlagsSdk.EnqueueEVP which is a no-op
        // when FeatureFlagsModule has not wired the delegate (e.g., killswitch disabled at the
        // module level or tracer not initialized). Dual-gated: provider skips hook registration,
        // AND FeatureFlagsModule skips creating FlagEvaluationApi.
        bool evpEnabled = !string.Equals(
            Environment.GetEnvironmentVariable("DD_FLAGGING_EVALUATION_COUNTS_ENABLED"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        if (evpEnabled)
        {
            _evpHook = new FlagEvalEVPHook();
        }
#endif
    }

    /// <summary> Gets a value indicating whether the Datadog's provider is instrumented and available  </summary>
    public static bool IsAvailable => FeatureFlagsSdk.IsAvailable();

    /// <summary> Notifies when a new config is available </summary>
    /// <param name="onNewConfig"> Action to be called </param>
    public static void RegisterOnNewConfigEventHandler(Action onNewConfig)
    {
        _onNewConfig = onNewConfig;
    }

    private void SignalGeneralUpdate()
    {
        try
        {
            _onNewConfig?.Invoke();

            // You don't have to provide specific flag keys
            var payload = new ProviderEventPayload
            {
                Message = "A backend update occurred, but specific changes are unknown."
            };

            EventChannel.Writer.TryWrite((object)payload);
        }
        catch { }
    }

    /// <summary> Gets provider metadata </summary>
    /// <returns> Returns provider metadata </returns>
    public override Metadata? GetMetadata() => _metadata;

    /// <summary> Resolve flag as boolean </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = FeatureFlagsSdk.Resolve<bool>(flagKey, Trace.FeatureFlags.ValueType.Boolean, defaultValue, context);
        return Task.FromResult(res);
    }

    /// <summary> Resolve flag as double </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = FeatureFlagsSdk.Resolve<double>(flagKey, Trace.FeatureFlags.ValueType.Numeric, defaultValue, context);
        return Task.FromResult(res);
    }

    /// <summary> Resolve flag as integer </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = FeatureFlagsSdk.Resolve<int>(flagKey, Trace.FeatureFlags.ValueType.Integer, defaultValue, context);
        return Task.FromResult(res);
    }

    /// <summary> Resolve flag as string </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = FeatureFlagsSdk.Resolve<string>(flagKey, Trace.FeatureFlags.ValueType.String, defaultValue, context);
        return Task.FromResult(res);
    }

    /// <summary> Resolve flag as Value </summary>
    /// <param name="flagKey"> Requested flag </param>
    /// <param name="defaultValue"> Default value </param>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancelation token </param>
    /// <returns> Returns the evaluation result </returns>
    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var res = FeatureFlagsSdk.Resolve<Value>(flagKey, Trace.FeatureFlags.ValueType.Json, defaultValue, context);
        return Task.FromResult(res);
    }

    /// <summary>
    /// Gets provider hooks for flag evaluation tracking.
    /// Returns: OTel FlagEvalMetricsHook (always) + FlagEvalEVPHook (when killswitch enabled).
    /// </summary>
    /// <returns> Returns the list of provider hooks. </returns>
    public override IImmutableList<Hook> GetProviderHooks()
    {
#if NET6_0_OR_GREATER
        if (_evpHook is not null)
        {
            return ImmutableList.Create<Hook>(_metricsHook, _evpHook);
        }

        return ImmutableList.Create<Hook>(_metricsHook);
#else
        return ImmutableList<Hook>.Empty;
#endif
    }

    /// <inheritdoc/>
    public void Dispose()
    {
#if NET6_0_OR_GREATER
        _metricsHook.Dispose();
#endif
    }
}
