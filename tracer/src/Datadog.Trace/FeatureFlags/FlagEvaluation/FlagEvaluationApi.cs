// <copyright file="FlagEvaluationApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// EVP flag evaluation writer — two-tier aggregation (full → degraded → drop-counted) with
/// a comparable canonical-context key and periodic flush to evp_proxy/v2/api/v2/flagevaluations.
///
/// Frozen-contract conformance (FANOUT-CONTRACT.md): two-tier (NO ultra-degraded),
/// comparable canonical-context key (sorted, type-tagged length-delimited — NOT a hash digest),
/// context pruned 256 fields/256 chars, caps globalCap=131072/perFlagCap=10000/degradedCap=32768,
/// eval-time from metadata key "dd.eval.timestamp_ms" (long) with DateTimeOffset.UtcNow fallback,
/// FinallyAsync hook, killswitch DD_FLAGGING_EVALUATION_COUNTS_ENABLED, NullValueHandling.Ignore
/// per tier (optional-field omission = schema-conformant for degraded tier).
/// </summary>
internal sealed class FlagEvaluationApi : IDisposable
{
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FlagEvaluationApi));

    /// <summary>EVP proxy path for flagevaluation events.</summary>
    public const string FlagEvaluationPath = "evp_proxy/v2/api/v2/flagevaluations";

    private static readonly JsonSerializerSettings SerializerSettings = new()
    {
        // Optional-field omission per tier: degraded tier leaves context/targeting_key null → omitted.
        // Reviewer concern #2: optional fields absent rather than null in JSON.
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        }
    };

    private readonly TaskCompletionSource<bool> _processExit = new();
    private readonly TimeSpan _sendInterval = TimeSpan.FromSeconds(10);

    private readonly FlagEvaluationAggregator _aggregator = new(
        globalCap: 131_072,
        perFlagCap: 10_000,
        degradedCap: 32_768);

    private IApiRequestFactory _apiRequestFactory;
    private string _service;
    private string _env;
    private string _version;
    private int _started;

    internal FlagEvaluationApi(TracerSettings tracerSettings)
    {
        UpdateApi(tracerSettings.Manager.InitialExporterSettings);
        UpdateContext(tracerSettings.Manager.InitialMutableSettings);

        tracerSettings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                UpdateApi(exporter);
            }

            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateContext(mutable);
            }
        });

        [MemberNotNull(nameof(_apiRequestFactory))]
        void UpdateApi(ExporterSettings exporterSettings)
        {
            Log.Debug("FlagEvaluationApi::UpdateApi -> Applying settings");
            var apiRequestFactory = AgentTransportStrategy.Get(
                exporterSettings,
                productName: "FeatureFlags evaluation",
                tcpTimeout: TimeSpan.FromSeconds(5),
                httpHeaderHelper: EventPlatformHeaderHelper.Instance);
            Interlocked.Exchange(ref _apiRequestFactory!, apiRequestFactory);
        }

        [MemberNotNull(nameof(_service), nameof(_env), nameof(_version))]
        void UpdateContext(MutableSettings settings)
        {
            Log.Debug("FlagEvaluationApi::UpdateContext -> Applying settings");
            Interlocked.Exchange(ref _service!, settings.DefaultServiceName);
            Interlocked.Exchange(ref _env!, settings.Environment ?? string.Empty);
            Interlocked.Exchange(ref _version!, settings.ServiceVersion ?? string.Empty);
        }
    }

    /// <summary>
    /// Non-blocking enqueue of one evaluation event. The background loop aggregates and flushes.
    /// This is the only method called from the hot-path FlagEvalEVPHook.FinallyAsync.
    /// </summary>
    public void Enqueue(FlagEvalEvent ev)
    {
        _aggregator.Add(ev);
        TryToStartSendLoopIfNotStarted();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _processExit.TrySetResult(true);
    }

    /// <summary>
    /// Builds the EVP payload from the aggregator's current state. Returns null when there is
    /// nothing to send (both maps empty). Drains the aggregator.
    /// Exposed as internal for unit testing; production callers use Enqueue + the send loop.
    /// </summary>
    internal static FlagEvaluationsRequest? BuildPayload(
        FlagEvaluationAggregator aggregator,
        string service,
        string env,
        string version)
    {
        DrainResult result = aggregator.Drain();
        Dictionary<FullKey, EvaluationEntry> full = result.Full;
        Dictionary<DegradedKey, EvaluationEntry> degraded = result.Degraded;
        long dropped = result.Dropped;

        if (dropped > 0)
        {
            Log.Warning<long>("FlagEvaluationApi: degraded aggregation tier full — dropped {Dropped} evaluation(s); raise degradedCap (best-effort telemetry)", dropped);
        }

        if (full.Count == 0 && degraded.Count == 0)
        {
            return null;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var events = new List<FlagEvaluationEvent>(full.Count + degraded.Count);

        // Full tier: all fields present including targeting_key and context.
        foreach (KeyValuePair<FullKey, EvaluationEntry> pair in full)
        {
            FullKey key = pair.Key;
            EvaluationEntry entry = pair.Value;

            var ev = new FlagEvaluationEvent
            {
                Timestamp = nowMs,
                Flag = new FlagEvalFlag { Key = key.FlagKey },
                FirstEvaluation = entry.FirstEvaluationMs,
                LastEvaluation = entry.LastEvaluationMs,
                EvaluationCount = entry.Count,
                RuntimeDefault = entry.RuntimeDefault ? (bool?)true : null,
                TargetingKey = StringUtil.IsNullOrEmpty(key.TargetingKey) ? null : key.TargetingKey,
                Variant = StringUtil.IsNullOrEmpty(key.Variant) ? null : new FlagEvalVariant { Key = key.Variant },
                Allocation = StringUtil.IsNullOrEmpty(key.AllocationKey) ? null : new FlagEvalAllocation { Key = key.AllocationKey },
                Context = entry.ContextAttrs is { Count: > 0 } ? new FlagEvalEventContext { Evaluation = entry.ContextAttrs } : null,
            };
            events.Add(ev);
        }

        // Degraded tier: no targeting_key, no context (reviewer concern #2: NullValueHandling.Ignore).
        foreach (KeyValuePair<DegradedKey, EvaluationEntry> pair in degraded)
        {
            DegradedKey key = pair.Key;
            EvaluationEntry entry = pair.Value;

            var ev = new FlagEvaluationEvent
            {
                Timestamp = nowMs,
                Flag = new FlagEvalFlag { Key = key.FlagKey },
                FirstEvaluation = entry.FirstEvaluationMs,
                LastEvaluation = entry.LastEvaluationMs,
                EvaluationCount = entry.Count,
                RuntimeDefault = entry.RuntimeDefault ? (bool?)true : null,
                Variant = StringUtil.IsNullOrEmpty(key.Variant) ? null : new FlagEvalVariant { Key = key.Variant },
                Allocation = StringUtil.IsNullOrEmpty(key.AllocationKey) ? null : new FlagEvalAllocation { Key = key.AllocationKey },
                // TargetingKey = null (omitted), Context = null (omitted) by NullValueHandling.Ignore
            };
            events.Add(ev);
        }

        return new FlagEvaluationsRequest
        {
            Context = new FlagEvalDDContext
            {
                Service = service,
                Env = StringUtil.IsNullOrEmpty(env) ? null : env,
                Version = StringUtil.IsNullOrEmpty(version) ? null : version,
            },
            FlagEvaluations = events,
        };
    }

    private void TryToStartSendLoopIfNotStarted()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(SendLoopAsync).ContinueWith(
            t => { Log.Error(t.Exception, "FeatureFlags FlagEvaluation send loop failed"); },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private async Task SendLoopAsync()
    {
        Log.Debug("FlagEvaluationApi::SendLoopAsync -> Enter");
        while (!_processExit.Task.IsCompleted)
        {
            try
            {
                var apiRequestFactory = _apiRequestFactory;
                var uri = apiRequestFactory.GetEndpoint(FlagEvaluationPath);
                var payload = BuildPayload(_aggregator, _service, _env, _version);
                if (payload is not null)
                {
                    var request = apiRequestFactory.Create(uri);
                    using var response = await request.PostAsJsonAsync(payload, MultipartCompression.GZip, SerializerSettings).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while sending Feature Flags evaluation events to the agent");
            }

            try
            {
                await Task.WhenAny(_processExit.Task, Task.Delay(_sendInterval)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutting down — ignore.
            }

            Log.Debug("FlagEvaluationApi::SendLoopAsync -> Exit");
        }
    }
}
