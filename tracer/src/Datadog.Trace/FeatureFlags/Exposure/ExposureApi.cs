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
    internal static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExposureApi));

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

    private Dictionary<string, string> _context;
    private Task? _sendLoopTask;
    private bool _disposed;

    internal ExposureApi(
        TracerSettings tracerSettings,
        FeatureFlagsEvpTransport transport,
        TimeSpan? sendInterval = null,
        TimeSpan? shutdownTimeout = null)
    {
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
            Log.Debug("ExposureApi::UpdateContext -> Applying settings");
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
            var completed = Task.WhenAny(sendLoopTask, Task.Delay(_shutdownTimeout)).GetAwaiter().GetResult();
            if (completed != sendLoopTask)
            {
                Log.Warning("Could not finish flushing Feature Flags exposures before process end");
            }
        }

        _settingsSubscription.Dispose();
    }

    private async Task SendLoopAsync()
    {
        Log.Debug("ExposureApi::SendLoopAsync -> Enter");
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
        await FlushAsync().ConfigureAwait(false);
        Log.Debug("ExposureApi::SendLoopAsync -> Exit");
    }

    private async Task FlushAsync()
    {
        try
        {
            var payload = TryGetPayload();
            if (payload is not null)
            {
                await _transport.SendAsync(payload, FeatureFlagsEvpTransport.ExposureIntakePath, SerializerSettings).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while sending Feature Flags exposures");
        }
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
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_exposureCache.Add(exposure))
            {
                lock (_exposures)
                {
                    _exposures.Enqueue(exposure);
                }
            }

            if (_sendLoopTask is null)
            {
                _sendLoopTask = Task.Run(SendLoopAsync);
                _sendLoopTask.ContinueWith(
                    t => Log.Error(t.Exception, "FeatureFlags Exposure send loop failed"),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }
        }
    }

    private sealed class ExposuresRequest(Dictionary<string, string> context, List<ExposureEvent> exposures)
    {
        public Dictionary<string, string> Context { get; } = context;

        public List<ExposureEvent> Exposures { get; } = exposures;
    }
}
