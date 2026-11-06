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
public sealed partial class DatadogProvider : global::OpenFeature.FeatureProvider, IDisposable
{
    // The status this provider last reported, which decides the transitions it owns. The first ready
    // event is not one of them: OpenFeature synthesizes one as soon as InitializeAsync returns, so
    // emitting it here as well would run every application handler twice.
    private const int StatusInitializing = 0;
    private const int StatusReady = 1;
    private const int StatusError = 2;

    private static readonly FeatureFlagsSdk.EvaluationCallback EvaluateDeferred = FeatureFlagsSdk.EvaluateDeferred;
    private static Action? _onNewConfig = null;
    private readonly FeatureFlagsSdk.EvaluationCallback? _evaluate;
    private readonly FeatureFlagsSdk.EvaluationCallback? _evaluateDeferred;
    private readonly Action _releaseFlagSelection;

    // Asked once: when the tracer supports it, exposures are sent only after OpenFeature completes the
    // evaluation. Otherwise the tracer sends them during the evaluation, as older providers expect.
    private readonly bool _deferExposures;
    private readonly ExposureCommitHook? _exposureCommitHook;
    private readonly Metadata _metadata = new Metadata("datadog-openfeature-provider");
#if NET6_0_OR_GREATER
    private readonly FlagEvalMetricsHook _metricsHook;
#endif

    // Span-enrichment hook is constructed ONLY when the gate is on; null otherwise so
    // nothing is allocated/registered when the feature is disabled.
    private readonly SpanEnrichmentHook? _spanEnrichmentHook;
    // Startup gate: enabling events later requires a new provider (or application restart).
    private readonly FlagEvalEVPHook? _evpHook;

    // Hooks are fixed at construction, so the list is built once per provider and reused for every evaluation.
    private readonly IImmutableList<Hook> _providerHooks;

    private int _status = StatusInitializing;
    private int _flagSelectionReleased;

    /// <summary> Initializes a new instance of the <see cref="DatadogProvider"/> class. </summary>
    public DatadogProvider()
        : this(new DatadogProviderOptions())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DatadogProvider"/> class with the supplied options.</summary>
    /// <param name="options">Options copied at construction.</param>
    public DatadogProvider(DatadogProviderOptions options)
        : this(options, FeatureFlagsSdk.SetFlagSelection, FeatureFlagsSdk.ReleaseFlagSelection, FeatureFlagsSdk.CanDeferExposures, FeatureFlagsSdk.CommitExposure)
    {
    }

    // Unit tests stand in for the tracer's flag selection entry points.
    internal DatadogProvider(DatadogProviderOptions options, Func<string[]?, string[]?, bool> setFlagSelection, Action releaseFlagSelection)
        : this(options, setFlagSelection, releaseFlagSelection, FeatureFlagsSdk.CanDeferExposures, FeatureFlagsSdk.CommitExposure)
    {
    }

    // The native profiler normally connects FeatureFlagsSdk.Evaluate to the tracer. Unit tests
    // can supply the real tracer evaluator directly while retaining provider argument/result mapping.
    internal DatadogProvider(FeatureFlagsSdk.EvaluationCallback evaluate)
        : this()
    {
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
    }

    // Unit tests stand in for both tracer entry points and for the capability probe.
    internal DatadogProvider(FeatureFlagsSdk.EvaluationCallback evaluate, FeatureFlagsSdk.EvaluationCallback evaluateDeferred, Func<bool> canDeferExposures, Func<IDictionary<string, string>, int> commitExposure)
        : this(new DatadogProviderOptions(), FeatureFlagsSdk.SetFlagSelection, FeatureFlagsSdk.ReleaseFlagSelection, canDeferExposures, commitExposure)
    {
        _evaluate = evaluate ?? throw new ArgumentNullException(nameof(evaluate));
        _evaluateDeferred = evaluateDeferred ?? throw new ArgumentNullException(nameof(evaluateDeferred));
    }

    private DatadogProvider(DatadogProviderOptions options, Func<string[]?, string[]?, bool> setFlagSelection, Action releaseFlagSelection, Func<bool> canDeferExposures, Func<IDictionary<string, string>, int> commitExposure)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Handed over at construction rather than at initialization, because an evaluation before
        // InitializeAsync also starts delivery. The lists are copied, so later changes have no effect.
        var selection = options.FlagSelection;
        setFlagSelection(selection?.Tags?.ToArray(), selection?.KeyPatterns?.ToArray());
        _releaseFlagSelection = releaseFlagSelection;

        FeatureFlagsSdk.RegisterOnNewConfigEventHandler(() => SignalGeneralUpdate());
        _deferExposures = canDeferExposures();
        if (_deferExposures)
        {
            _exposureCommitHook = new ExposureCommitHook(commitExposure);
        }

#if NET6_0_OR_GREATER
        _metricsHook = new FlagEvalMetricsHook();
#endif
        if (FeatureFlagsSdk.IsSpanEnrichmentEnabled())
        {
            _spanEnrichmentHook = new SpanEnrichmentHook();
        }

        if (FeatureFlagsSdk.IsEvaluationEventsEnabled())
        {
            _evpHook = new FlagEvalEVPHook();
        }

        _providerHooks = CreateProviderHooks();
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

    /// <summary>
    /// Shuts the provider down. A provider holds the process's flag selection from its construction
    /// until it shuts down, so once every provider has, the next one constructed selects its own flags.
    /// </summary>
    /// <param name="cancellationToken"> Async cancellation token </param>
    /// <returns> A task that completes when the provider has shut down </returns>
    public override Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _flagSelectionReleased, 1) == 0)
        {
            _releaseFlagSelection();
        }

        return base.ShutdownAsync(cancellationToken);
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
        var res = FeatureFlagsSdk.Resolve<bool>(flagKey, Trace.FeatureFlags.ValueType.Boolean, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
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
        var res = FeatureFlagsSdk.Resolve<double>(flagKey, Trace.FeatureFlags.ValueType.Numeric, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
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
        var res = FeatureFlagsSdk.Resolve<int>(flagKey, Trace.FeatureFlags.ValueType.Integer, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
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
        var res = FeatureFlagsSdk.Resolve<string>(flagKey, Trace.FeatureFlags.ValueType.String, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
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
        var res = FeatureFlagsSdk.Resolve<Value>(flagKey, Trace.FeatureFlags.ValueType.Json, defaultValue, context, Evaluator(), _evpHook, _deferExposures);
        return Task.FromResult(res);
    }

    /// <summary> Gets provider hooks for flag evaluation metrics tracking. </summary>
    /// <returns> Returns the list of provider hooks. </returns>
    public override IImmutableList<Hook> GetProviderHooks() => _providerHooks;

    /// <inheritdoc/>
    public void Dispose()
    {
#if NET6_0_OR_GREATER
        _metricsHook.Dispose();
#endif
        // The span-enrichment hook owns no resources and per-trace enrichment state is released with
        // the trace context, so there's nothing to dispose on provider close.
    }

    private FeatureFlagsSdk.EvaluationCallback? Evaluator() => _deferExposures ? _evaluateDeferred ?? EvaluateDeferred : _evaluate;

    private IImmutableList<Hook> CreateProviderHooks()
    {
        var hooks = ImmutableList.CreateBuilder<Hook>();
#if NET6_0_OR_GREATER
        hooks.Add(_metricsHook);
#endif
        if (_spanEnrichmentHook is not null)
        {
            hooks.Add(_spanEnrichmentHook);
        }

        if (_evpHook is not null)
        {
            hooks.Add(_evpHook);
        }

        // Finally hooks run in reverse order, so the last hook commits before the others read the result.
        if (_exposureCommitHook is not null)
        {
            hooks.Add(_exposureCommitHook);
        }

        return hooks.ToImmutable();
    }
}
