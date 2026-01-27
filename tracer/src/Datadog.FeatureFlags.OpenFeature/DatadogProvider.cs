// <copyright file="DatadogProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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
public sealed class DatadogProvider : global::OpenFeature.FeatureProvider
{
    private static Action? _onNewConfig = null;
    private Metadata _metadata = new Metadata("datadog-openfeature-provider");

    /// <summary> Initializes a new instance of the <see cref="DatadogProvider"/> class. </summary>
    public DatadogProvider()
    {
        FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => SignalGeneralUpdate());
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
}
