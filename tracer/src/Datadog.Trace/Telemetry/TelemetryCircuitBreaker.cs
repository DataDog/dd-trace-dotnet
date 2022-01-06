// <copyright file="TelemetryCircuitBreaker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Telemetry;

internal class TelemetryCircuitBreaker
{
    internal const int MaxFatalErrors = 2;
    internal const int MaxTransientErrors = 5;

    private bool _hasSentSuccessfully = false;
    private int _initialFatalCount = 0;
    private int _failureCount = 0;

    public ConfigTelemetryData PreviousConfiguration { get; private set; }

    public ICollection<DependencyTelemetryData> PreviousDependencies { get; private set; }

    public ICollection<IntegrationTelemetryData> PreviousIntegrations { get; private set; }

    public TelemetryPushResult Evaluate(
        TelemetryPushResult result,
        ConfigTelemetryData config,
        ICollection<DependencyTelemetryData> dependencies,
        ICollection<IntegrationTelemetryData> integrations)
    {
        if (result == TelemetryPushResult.Success)
        {
            _hasSentSuccessfully = true;
            _failureCount = 0;
            PreviousConfiguration = null;
            PreviousDependencies = null;
            PreviousIntegrations = null;
            return result;
        }
        else if (result == TelemetryPushResult.FatalError && !_hasSentSuccessfully)
        {
            _initialFatalCount++;
            if (_initialFatalCount >= MaxFatalErrors)
            {
                // we've had MaxFatalErrors 404s, 1 minute apart, prob never going to work.
                PreviousConfiguration = null;
                PreviousDependencies = null;
                PreviousIntegrations = null;
                return TelemetryPushResult.FatalError;
            }
        }
        else
        {
            _failureCount++;
            if (_failureCount >= MaxTransientErrors)
            {
                // we've had MaxTransientErrors in a row, 1 minute apart, probably something bigger wrong
                PreviousConfiguration = null;
                PreviousDependencies = null;
                PreviousIntegrations = null;
                return TelemetryPushResult.FatalError;
            }
        }

        // We should retry next time
        PreviousConfiguration = config;
        PreviousDependencies = dependencies;
        PreviousIntegrations = integrations;
        return TelemetryPushResult.TransientFailure;
    }
}
