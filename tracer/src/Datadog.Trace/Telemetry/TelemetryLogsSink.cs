// <copyright file="TelemetryLogsSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission.Formatting;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Telemetry;

internal class TelemetryLogsSink : BatchingSink<LogMessageData>, ITelemetryLogsSink
{
    private readonly bool _deDuplicationEnabled;
    private ConcurrentDictionary<uint, int>? _logCounts;
    private ConcurrentDictionary<uint, int>? _logCountsReserve;
    private TelemetryConfiguration? _configuration;
    private int _currentTransport = 0;

    public TelemetryLogsSink(
        BatchingSinkOptions sinkOptions,
        Action disableSinkAction,
        IDatadogLogger log,
        bool deDuplicationEnabled)
        : base(sinkOptions, disableSinkAction, log)
    {
        _deDuplicationEnabled = deDuplicationEnabled;
        if (_deDuplicationEnabled)
        {
            _logCounts = new();
            _logCountsReserve = new();
        }
    }

    /// <summary>
    /// Enqueue the log. If the log is not new, and de-duplication is enabled, increment the count and don't enqueue the log
    /// </summary>
    public override void EnqueueLog(LogMessageData logEvent)
    {
        if (_deDuplicationEnabled)
        {
            var eventId = EventIdHash.Compute(logEvent.Message ?? string.Empty, logEvent.StackTrace);
            ConcurrentDictionary<uint, int> logCounts = _logCounts!;

            var newCount = logCounts.AddOrUpdate(eventId, addValue: 1, updateValueFactory: static (_, prev) => prev + 1);

            if (newCount != 1)
            {
                // already have this log, so don't enqueue it again
                return;
            }
        }

        base.EnqueueLog(logEvent);
    }

    /// <summary>
    /// Need to initialize the sink _before_ we have access to these
    /// So we provide these only when we create telemetry
    /// </summary>
    public void Initialize(
        ApplicationTelemetryData applicationData,
        HostTelemetryData hostData,
        TelemetryDataBuilder dataBuilder,
        ITelemetryTransport[] transports)
    {
        // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (applicationData is null || hostData is null)
        {
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            // These must never be null, but it may not be safe to throw
            // so just silently ignore if they are
            return;
        }

        Interlocked.Exchange(ref _configuration, new TelemetryConfiguration(applicationData, hostData, dataBuilder, transports));
        Start();
    }

    protected override async Task<bool> EmitBatch(Queue<LogMessageData> events)
    {
        try
        {
            if (events.Count == 0 || _configuration is not { } telemetry)
            {
                // We should never hit this, as we don't start emitting until we are initialized,
                return true;
            }

            if (_deDuplicationEnabled)
            {
                var logCounts = Interlocked.Exchange(ref _logCounts, _logCountsReserve)!;
                foreach (var logEvent in events)
                {
                    // modify the message to add the final log count
                    var eventId = EventIdHash.Compute(logEvent.Message ?? string.Empty, logEvent.StackTrace);
                    if (logCounts.TryGetValue(eventId, out var count))
                    {
                        logEvent.Message = $"{logEvent.Message}. {count - 1} additional messages skipped.";
                    }
                }

                logCounts.Clear();
                _logCountsReserve = logCounts;
            }

            var logs = new LogsPayload(events.Count);
            logs.AddRange(events);

            var telemetryData = telemetry.DataBuilder.BuildLogsTelemetryData(telemetry.ApplicationData, telemetry.HostData, logs);

            // Try sending with each of the transports, starting with the last transport that succeeded
            // A fatal error when sending means we don't try again.
            // Note that this means if (for example) we try sending to an agent that doesn't
            // support telemetry, we get a fatal error, and we WONT try sending to it again,
            // even if the agent is upgraded in the background.
            var transports = telemetry.Transports;
            for (var i = 0; i < transports.Length; i++)
            {
                var transport = transports[_currentTransport];
                if (transport.IsEnabled)
                {
                    var success = await transport.Transport.PushTelemetry(telemetryData).ConfigureAwait(false);
                    switch (success)
                    {
                        case TelemetryPushResult.Success:
                            return true;
                        case TelemetryPushResult.FatalError:
                            transport.IsEnabled = false;
                            break;
                        case TelemetryPushResult.TransientFailure:
                        default:
                            break;
                    }
                }

                _currentTransport = (_currentTransport + 1) % transports.Length;
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal class TransportStatus
    {
        public TransportStatus(ITelemetryTransport transport)
        {
            Transport = transport;
            IsEnabled = true;
        }

        public ITelemetryTransport Transport { get; }

        public bool IsEnabled { get; set; }
    }

    public class TelemetryConfiguration
    {
        public TelemetryConfiguration(
            ApplicationTelemetryData applicationData,
            HostTelemetryData hostData,
            TelemetryDataBuilder dataBuilder,
            ITelemetryTransport[] transports)
        {
            ApplicationData = applicationData;
            HostData = hostData;
            DataBuilder = dataBuilder;
            Transports = new TransportStatus[transports.Length];

            for (var i = 0; i < transports.Length; i++)
            {
                Transports[i] = new TransportStatus(transports[i]);
            }
        }

        public ApplicationTelemetryData ApplicationData { get; }

        public HostTelemetryData HostData { get; }

        public TransportStatus[] Transports { get; }

        public TelemetryDataBuilder DataBuilder { get; }
    }
}
