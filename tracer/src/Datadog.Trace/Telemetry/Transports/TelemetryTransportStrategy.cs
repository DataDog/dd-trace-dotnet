// <copyright file="TelemetryTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry.Transports;

internal static class TelemetryTransportStrategy
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TelemetryTransportStrategy));
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static IApiRequestFactory GetDirectIntakeFactory(TelemetrySettings.AgentlessSettings settings)
    {
#if NETCOREAPP
        Log.Information("Using {FactoryType} for telemetry transport direct to intake.", nameof(HttpClientRequestFactory));
        return new HttpClientRequestFactory(settings.AgentlessUri, TelemetryHttpHeaderNames.GetDefaultIntakeHeaders(settings), timeout: Timeout);
#else
        Log.Information("Using {FactoryType} for telemetry transport direct to intake.", nameof(ApiWebRequestFactory));
        return new ApiWebRequestFactory(settings.AgentlessUri, TelemetryHttpHeaderNames.GetDefaultIntakeHeaders(settings), timeout: Timeout);
#endif
    }

    public static IApiRequestFactory GetAgentIntakeFactory(ImmutableExporterSettings settings)
        => AgentTransportStrategy.Get(
            settings,
            productName: "telemetry",
            tcpTimeout: Timeout,
            TelemetryHttpHeaderNames.GetDefaultAgentHeaders(),
            () => new TelemetryAgentHttpHeaderHelper(),
            uri => UriHelpers.Combine(uri, TelemetryConstants.AgentTelemetryEndpoint));
}
