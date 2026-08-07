// <copyright file="DebuggerTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Upload;

internal static class DebuggerTransportFactory
{
    internal const string DefaultAgentlessRelativePath = "/api/v2/debugger";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerTransportFactory));

    internal static DebuggerTransportInfo? CreateForDynamicInstrumentation(TracerSettings tracerSettings, DebuggerSettings settings, IDiscoveryService discoveryService)
    {
        if (!settings.IsDynamicInstrumentationAgentlessLocalMode)
        {
            return CreateAgentTransport(tracerSettings, discoveryService);
        }

        if (StringUtil.IsNullOrWhiteSpace(settings.DynamicInstrumentationAgentlessApiKey))
        {
            Log.ErrorSkipTelemetry("Dynamic Instrumentation agentless uploads enabled but DD_API_KEY is not set. Disabling Dynamic Instrumentation.");
            return null;
        }

        var baseUri = new Uri($"https://debugger-intake.{settings.DynamicInstrumentationAgentlessSite}");

        var apiFactory = DebuggerTransportStrategy.Get(
            baseUri,
            [
                ..AgentHttpHeaderNames.DefaultHeaders,
                new KeyValuePair<string, string>("DD-API-KEY", settings.DynamicInstrumentationAgentlessApiKey),
                new KeyValuePair<string, string>("DD-EVP-ORIGIN", "dd-trace-dotnet")
            ]);

        return new DebuggerTransportInfo(apiFactory, discoveryService: null, DefaultAgentlessRelativePath, isAgentless: true);
    }

    private static DebuggerTransportInfo CreateAgentTransport(TracerSettings tracerSettings, IDiscoveryService discoveryService)
    {
        var apiFactory = AgentTransportStrategy.Get(
            tracerSettings.Manager.InitialExporterSettings,
            productName: "debugger",
            tcpTimeout: TimeSpan.FromSeconds(15),
            httpHeaderHelper: MinimalAgentHeaderHelper.Instance);

        return new DebuggerTransportInfo(apiFactory, discoveryService, staticEndpoint: null, isAgentless: false);
    }
}
