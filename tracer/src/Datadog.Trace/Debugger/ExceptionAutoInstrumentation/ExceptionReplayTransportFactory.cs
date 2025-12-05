// <copyright file="ExceptionReplayTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ExceptionReplayTransportFactory
    {
        private const string DefaultRelativePath = "/api/v2/debugger";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionReplayTransportFactory));

        internal static ExceptionReplayTransportInfo? Create(TracerSettings tracerSettings, ExceptionReplaySettings settings, IDiscoveryService discoveryService)
        {
            if (!settings.AgentlessEnabled)
            {
                return CreateAgentTransport(tracerSettings, discoveryService);
            }

            if (StringUtil.IsNullOrWhiteSpace(settings.AgentlessApiKey))
            {
                Log.ErrorSkipTelemetry("Exception Replay agentless uploads enabled but DD_API_KEY is not set. Disabling Exception Replay.");
                return null;
            }

            if (!TryResolveAgentlessEndpoint(settings, out var baseUri, out var relativePath))
            {
                Log.ErrorSkipTelemetry("Exception Replay agentless uploads enabled but a valid intake URL could not be determined. Disabling Exception Replay.");
                return null;
            }

            var apiFactory = DebuggerTransportStrategy.Get(
                baseUri,
                [
                    ..AgentHttpHeaderNames.DefaultHeaders,
                    new KeyValuePair<string, string>("DD-API-KEY", settings.AgentlessApiKey),
                    new KeyValuePair<string, string>("DD-EVP-ORIGIN", "dd-trace-dotnet")
                ]);
            return new ExceptionReplayTransportInfo(apiFactory, null, relativePath, isAgentless: true);
        }

        private static ExceptionReplayTransportInfo CreateAgentTransport(TracerSettings tracerSettings, IDiscoveryService discoveryService)
        {
            var apiFactory = Agent.AgentTransportStrategy.Get(
                tracerSettings.Manager.InitialExporterSettings,
                productName: "exception-replay",
                tcpTimeout: TimeSpan.FromSeconds(15),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);

            return new ExceptionReplayTransportInfo(apiFactory, discoveryService, staticEndpoint: null, isAgentless: false);
        }

        private static bool TryResolveAgentlessEndpoint(ExceptionReplaySettings settings, [NotNullWhen(true)] out Uri? baseUri, [NotNullWhen(true)] out string relativePath)
        {
            var overrideUrl = settings.AgentlessUrlOverride;
            if (string.IsNullOrWhiteSpace(overrideUrl))
            {
                baseUri = new Uri($"https://debugger-intake.{settings.AgentlessSite}");
                relativePath = DefaultRelativePath;
                return true;
            }

            if (!Uri.TryCreate(overrideUrl, UriKind.Absolute, out var uri))
            {
                baseUri = null;
                relativePath = string.Empty;
                return false;
            }

            baseUri = new Uri($"{uri.Scheme}://{uri.Authority}");
            var path = uri.PathAndQuery;
            relativePath = string.IsNullOrEmpty(path) ? DefaultRelativePath : EnsureLeadingSlash(path);
            return true;
        }

        private static string EnsureLeadingSlash(string value)
        {
            return value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
        }
    }
}
