// <copyright file="TelemetryLogsSink.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Telemetry;

internal class TelemetryLogsSink : BatchingSink<LogMessageData>, ITelemetryLogsSink
{
    private TelemetryConfiguration? _configuration;
    private int _currentTransport = 0;

    public TelemetryLogsSink(
        BatchingSinkOptions sinkOptions,
        Action disableSinkAction,
        IDatadogLogger log)
        : base(sinkOptions, disableSinkAction, log)
    {
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
