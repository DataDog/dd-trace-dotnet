// <copyright file="TelemetryTransportManagerV2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Telemetry;

internal class TelemetryTransportManagerV2
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TelemetryTransportManagerV2>();

    internal const int MaxFatalErrors = 2;
    internal const int MaxTransientErrors = 5;
    private readonly ITelemetryTransport[] _transports;

    private bool _hasSentSuccessfully = false;
    private int _initialFatalCount = 0;
    private int _failureCount = 0;
    private int _currentTransport = 0;

    public TelemetryTransportManagerV2(ITelemetryTransport[] transports)
    {
        _transports = transports ?? throw new ArgumentNullException(nameof(transports));
        if (transports.Length > 0 && Log.IsEnabled(LogEventLevel.Debug))
        {
            var firstTransport = transports[0];
            Log.Debug<int, string>(
                "Telemetry configured with {TransportCount} transports. Initial transport {TransportInfo}",
                transports.Length,
                firstTransport.GetTransportInfo());
        }
    }

    public async Task<TelemetryTransportResult> TryPushTelemetry(TelemetryDataV2 telemetryData)
    {
        if (_currentTransport >= _transports.Length)
        {
            return TelemetryTransportResult.FatalError;
        }

        var transport = _transports[_currentTransport];

        var pushResult = await transport.PushTelemetry(telemetryData).ConfigureAwait(false);

        return EvaluateCircuitBreaker(pushResult);
    }

    private TelemetryTransportResult EvaluateCircuitBreaker(TelemetryPushResult result)
    {
        if (result == TelemetryPushResult.Success)
        {
            _hasSentSuccessfully = true;
            _failureCount = 0;
            return TelemetryTransportResult.Success;
        }
        else if (result == TelemetryPushResult.FatalError && !_hasSentSuccessfully)
        {
            _initialFatalCount++;
            if (_initialFatalCount >= MaxFatalErrors)
            {
                // we've had MaxFatalErrors 404s, 1 minute apart, prob never going to work, so try next transport
                return ConfigureNextTransport();
            }
        }
        else
        {
            _failureCount++;
            if (_failureCount >= MaxTransientErrors)
            {
                // we've had MaxTransientErrors in a row, 1 minute apart, probably something bigger wrong
                return ConfigureNextTransport();
            }
        }

        // We should retry next time
        return TelemetryTransportResult.TransientError;
    }

    private TelemetryTransportResult ConfigureNextTransport()
    {
        // if we have more transports available, treat this as a transient error
        // otherwise it's fatal
        _currentTransport++;

        if (_currentTransport < _transports.Length)
        {
            Log.Debug(
                "Telemetry transport failed. Using next transport {TransportInfo}",
                _transports[_currentTransport].GetTransportInfo());

            // reset the circuit breaker counters
            _failureCount = 0;
            _initialFatalCount = 0;
            _hasSentSuccessfully = false;
            // try with the next transport next time
            return TelemetryTransportResult.TransientError;
        }

        return TelemetryTransportResult.FatalError; // we're out of transports
    }
}
