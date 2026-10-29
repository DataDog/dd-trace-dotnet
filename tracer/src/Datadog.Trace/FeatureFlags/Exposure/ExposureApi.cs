// <copyright file="ExposureApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.FeatureFlags.Exposure.Model;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Serialization;

namespace Datadog.Trace.FeatureFlags.Exposure;

internal sealed class ExposureApi : IDisposable
{
    private const int DefaultCapacity = 1 << 16; // 65536 elements
    private static readonly TimeSpan DefaultSendInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DefaultShutdownTimeout = TimeSpan.FromSeconds(10);

    [TestingAndPrivateOnly]
    internal static readonly JsonSerializerSettings SerializerSettings = new()
    {
        NullValueHandling = NullValueHandling.Include,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        }
    };

    private readonly TaskCompletionSource<bool> _processExit = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _lifecycleLock = new();
    private readonly TimeSpan _sendInterval;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Queue<ExposureEvent> _exposures = new Queue<ExposureEvent>();
    private readonly ExposureCache _exposureCache = new ExposureCache(DefaultCapacity);
    private readonly FeatureFlagsEvpTransport _transport;
    private readonly IDisposable _settingsSubscription;
    private readonly IDatadogLogger _log;

    private Dictionary<string, string> _context;
    private Task? _sendLoopTask;
    private bool _disposed;

    internal ExposureApi(
        TracerSettings tracerSettings,
        FeatureFlagsEvpTransport transport,
        TimeSpan? sendInterval = null,
        TimeSpan? shutdownTimeout = null,
        IDatadogLogger? logger = null)
    {
        _log = logger ?? DatadogLogging.GetLoggerFor(typeof(ExposureApi));
        _transport = transport;
        _sendInterval = sendInterval ?? DefaultSendInterval;
        _shutdownTimeout = shutdownTimeout ?? DefaultShutdownTimeout;
        UpdateContext(tracerSettings.Manager.InitialMutableSettings);

        _settingsSubscription = tracerSettings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateContext(mutable);
            }
        });

        [MemberNotNull(nameof(_context))]
        void UpdateContext(MutableSettings settings)
        {
            _log.Debug("ExposureApi::UpdateContext -> Applying settings");
            var context = new Dictionary<string, string>
            {
                { "service", settings.DefaultServiceName },
                { "env", settings.Environment ?? "unknown" },
                { "version", settings.ServiceVersion ?? "unknown" }
            };
            Interlocked.Exchange(ref _context!, context);
        }
    }

    public void Dispose()
    {
        Task? sendLoopTask;
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _processExit.TrySetResult(true);
            sendLoopTask = _sendLoopTask;
        }

        if (sendLoopTask is not null)
        {
            // A timed wait still expires when ThreadPool timers cannot run. WhenAny prevents a
            // faulted or canceled send loop from propagating an exception out of Dispose.
            if (!Task.WhenAny(sendLoopTask).Wait(_shutdownTimeout))
            {
                _log.Warning<double>("Could not finish flushing Feature Flags exposures within {TimeoutMs}ms before process end; exposures not yet delivered may be lost", _shutdownTimeout.TotalMilliseconds);
            }
        }

        _settingsSubscription.Dispose();
    }

    private async Task SendLoopAsync()
    {
        _log.Debug("ExposureApi::SendLoopAsync -> Enter");
        while (!_processExit.Task.IsCompleted)
        {
            await FlushAsync().ConfigureAwait(false);

            try
            {
                await Task.WhenAny(_processExit.Task, Task.Delay(_sendInterval)).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // We are shutting down, so don't do anything about it
            }
        }

        // Dispose signals the loop and then waits for this bounded final flush. This prevents the
        // common short-lived-process loss mode without allowing shutdown to hang indefinitely.
        var flushed = await FlushAsync().ConfigureAwait(false);
        _log.Debug<int>("Feature Flags: final exposure flush handed {ExposureCount} exposure(s) to the transport; see transport outcome", flushed);
        _log.Debug("ExposureApi::SendLoopAsync -> Exit");
    }

    /// <summary>
    /// Sends what is queued, and returns how many exposures it handed to the transport. Handing them
    /// over is not delivery: the transport reports the outcome of the request.
    /// </summary>
    private async Task<int> FlushAsync()
    {
        try
        {
            var payload = TryGetPayload();
            if (payload is not null)
            {
                _log.Debug<int>("Feature Flags: sending a batch of {ExposureCount} exposure(s)", payload.Exposures.Count);
                await _transport.SendAsync(payload, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings).ConfigureAwait(false);
                return payload.Exposures.Count;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while sending Feature Flags exposures");
        }

        return 0;
    }

    private ExposuresRequest? TryGetPayload()
    {
        List<ExposureEvent> exposures;
        lock (_exposures)
        {
            if (_exposures.Count == 0)
            {
                // nothing to do, skip send
                return null;
            }

            exposures = [.. _exposures];
            _exposures.Clear();
        }

        return new ExposuresRequest(_context, exposures);
    }

    public void SendExposure(in ExposureEvent exposure)
    {
        bool stopped;
        var queued = false;

        lock (_lifecycleLock)
        {
            stopped = _disposed;
            if (!stopped)
            {
                queued = _exposureCache.Add(exposure);
                if (queued)
                {
                    lock (_exposures)
                    {
                        _exposures.Enqueue(exposure);
                    }
                }

                if (_sendLoopTask is null)
                {
                    _sendLoopTask = Task.Run(SendLoopAsync);
                    _ = _sendLoopTask.ContinueWith(
                        t => _log.Error(t.Exception, "FeatureFlags Exposure send loop failed"),
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted,
                        TaskScheduler.Default);
                }
            }
        }

        // Logged after the lock is released, so writing the line never holds up another evaluation.
        // Queued is not delivered: the flush that sends it reports the outcome.
        if (stopped)
        {
            _log.Debug<string>("Feature Flags: exposure for {FlagKey} dropped: exposure reporting has stopped", exposure.Flag.Key);
        }
        else if (queued)
        {
            _log.Debug<string, string, string, string>(
                "Feature Flags: exposure for {FlagKey} queued for the next flush (allocation={AllocationKey}, variant={VariantKey}, subject={TargetingKey})",
                exposure.Flag.Key,
                exposure.Allocation.Key,
                exposure.Variant.Key,
                exposure.Subject.Id);
        }
        else
        {
            _log.Debug<string, string>(
                "Feature Flags: exposure for {FlagKey} not queued: subject {TargetingKey} already has an identical exposure recorded",
                exposure.Flag.Key,
                exposure.Subject.Id);
        }
    }

    private sealed class ExposuresRequest(Dictionary<string, string> context, List<ExposureEvent> exposures)
    {
        public Dictionary<string, string> Context { get; } = context;

        public List<ExposureEvent> Exposures { get; } = exposures;
    }
}
