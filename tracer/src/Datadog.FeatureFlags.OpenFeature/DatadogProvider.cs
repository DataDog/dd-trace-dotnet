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
using OpenFeature.Error;
using OpenFeature.Model;

namespace Datadog.FeatureFlags.OpenFeature;

/// <summary>
/// OpenFeature V2.0.0+ Provider for Datadog
/// </summary>
public sealed class DatadogProvider : global::OpenFeature.FeatureProvider, IDisposable
{
    // The status this provider last reported, which decides the transitions it owns. The first ready
    // event is not one of them: OpenFeature synthesizes one as soon as InitializeAsync returns, so
    // emitting it here as well would run every application handler twice.
    private const int StatusInitializing = 0;
    private const int StatusReady = 1;
    private const int StatusError = 2;

    private static Action? _onNewConfig = null;
    private readonly Metadata _metadata = new Metadata("datadog-openfeature-provider");
#if NET6_0_OR_GREATER
    private readonly FlagEvalMetricsHook _metricsHook;
#endif

    // Span-enrichment hook is constructed ONLY when the gate is on; null otherwise so
    // nothing is allocated/registered when the feature is disabled.
    private readonly SpanEnrichmentHook? _spanEnrichmentHook;

    private int _status = StatusInitializing;

    /// <summary> Initializes a new instance of the <see cref="DatadogProvider"/> class. </summary>
    public DatadogProvider()
    {
        FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => SignalGeneralUpdate());
#if NET6_0_OR_GREATER
        _metricsHook = new FlagEvalMetricsHook();
#endif
        if (FeatureFlagsSdk.IsSpanEnrichmentEnabled())
        {
            _spanEnrichmentHook = new SpanEnrichmentHook();
        }
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
        // The status event is written before anything the application supplied runs: a handler that
        // throws must not be able to suppress it.
        try
        {
            if (!FeatureFlagsSdk.HasConfiguration())
            {
                SignalConfigurationUnavailable();
            }
            else if (Interlocked.CompareExchange(ref _status, StatusReady, StatusError) == StatusError)
            {
                // Configuration is back after a withdrawal, and nothing else promotes the provider:
                // OpenFeature keeps the error status until a ready event replaces it.
                SignalReady();
            }
            else if (Volatile.Read(ref _status) == StatusReady)
            {
                // Specific flag keys are unknown, so this payload only reports that something changed.
                // An event already queued says exactly the same thing, which is why a full channel is
                // left alone: the notification is on its way regardless.
                EventChannel.Writer.TryWrite(CreatePayload(ProviderEventTypes.ProviderConfigurationChanged, "A backend update occurred, but specific changes are unknown."));
            }

            // Otherwise initialization has not returned yet, and the ready event it produces already
            // accounts for this configuration.
        }
        catch { }

        try
        {
            _onNewConfig?.Invoke();
        }
        catch { }
    }

    private void SignalConfigurationUnavailable()
    {
        // Configuration was withdrawn, so every evaluation now returns its default value. Leaving the
        // status at READY would report a provider that resolves nothing, so an error is emitted; the
        // next configuration promotes the provider back.
        if (Interlocked.CompareExchange(ref _status, StatusError, StatusReady) != StatusReady)
        {
            // Not reported as ready, so there is no status to correct.
            return;
        }

        var payload = CreatePayload(ProviderEventTypes.ProviderError, "Feature flag configuration is unavailable.");
        payload.ErrorType = ErrorType.ProviderNotReady;

        if (!EventChannel.Writer.TryWrite(payload))
        {
            // A status transition cannot be dropped, for the same reason the ready event cannot.
            _ = WriteWhenRoomAvailableAsync(payload);
        }
    }

    private void SignalReady()
    {
        var payload = CreatePayload(ProviderEventTypes.ProviderReady, "Feature flag configuration was received.");

        if (EventChannel.Writer.TryWrite(payload))
        {
            return;
        }

        // Unlike a change notification, this one cannot be dropped: losing it would leave the provider
        // reported as errored even though it can now resolve flags. The channel holds a single item, so
        // waiting for room happens in the background rather than on the thread applying the
        // configuration.
        _ = WriteWhenRoomAvailableAsync(payload);
    }

    private async Task WriteWhenRoomAvailableAsync(ProviderEventPayload payload)
    {
        try
        {
            await EventChannel.Writer.WriteAsync(payload).ConfigureAwait(false);
        }
        catch
        {
            // The channel is completed when the provider is replaced or the SDK shuts down, at which
            // point this provider's status no longer matters.
        }
    }

    private ProviderEventPayload CreatePayload(ProviderEventTypes type, string message)
        => new ProviderEventPayload
        {
            Type = type,
            Message = message,
            ProviderName = _metadata.Name,
        };

    /// <summary>
    /// Starts flag configuration delivery and waits for the first configuration, so that a ready
    /// provider can actually resolve flags.
    /// <para>
    /// It returns normally when the wait times out, because slow delivery is transient and the
    /// configuration still arrives afterwards. It throws <see cref="ProviderFatalException"/> when no
    /// source could start delivery at all, which leaves the provider unable to resolve anything for
    /// the rest of the process: that is irrecoverable rather than merely not-ready. The exception does
    /// not reach the application, because OpenFeature handles it while setting the provider.
    /// </para>
    /// <para>
    /// No status event is emitted here. OpenFeature derives the provider's initial status from how
    /// this method returns, so the provider only reports the transitions that happen afterwards.
    /// </para>
    /// </summary>
    /// <param name="context"> Evaluation context </param>
    /// <param name="cancellationToken"> Async cancellation token </param>
    /// <returns> A task that completes when initialization is complete </returns>
    public override async Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await FeatureFlagsSdk.InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _status, StatusError);

            // Irrecoverable: no source can be started later in this process, so no configuration will
            // ever arrive and every evaluation returns its default value. OpenFeature reports the error
            // status from this exception.
            throw new ProviderFatalException(exception.Message, exception);
        }

        // Recorded rather than signalled, because OpenFeature marks the provider ready as soon as this
        // returns. It is what lets a later withdrawal be reported as a transition out of READY.
        Volatile.Write(ref _status, StatusReady);
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

    /// <summary> Gets provider hooks for flag evaluation metrics tracking. </summary>
    /// <returns> Returns the list of provider hooks. </returns>
    public override IImmutableList<Hook> GetProviderHooks()
    {
#if NET6_0_OR_GREATER
        if (_spanEnrichmentHook is not null)
        {
            return ImmutableList.Create<Hook>(_metricsHook, _spanEnrichmentHook);
        }

        return ImmutableList.Create<Hook>(_metricsHook);
#else
        if (_spanEnrichmentHook is not null)
        {
            return ImmutableList.Create<Hook>(_spanEnrichmentHook);
        }

        return ImmutableList<Hook>.Empty;
#endif
    }

    /// <inheritdoc/>
    public void Dispose()
    {
#if NET6_0_OR_GREATER
        _metricsHook.Dispose();
#endif
        // The span-enrichment hook owns no resources and per-trace enrichment state is released with
        // the trace context, so there's nothing to dispose on provider close.
    }
}
