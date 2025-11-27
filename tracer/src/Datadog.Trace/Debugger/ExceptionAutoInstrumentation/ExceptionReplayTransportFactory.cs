// <copyright file="ExceptionReplayTransportFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    internal static class ExceptionReplayTransportFactory
    {
        private const string DefaultRelativePath = "/api/v2/debugger";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ExceptionReplayTransportFactory));

        internal static ExceptionReplayTransportInfo Create(TracerSettings tracerSettings, ExceptionReplaySettings settings, IDiscoveryService discoveryService)
        {
            if (!settings.AgentlessEnabled)
            {
                return CreateAgentTransport(tracerSettings, discoveryService);
            }

            if (string.IsNullOrWhiteSpace(settings.AgentlessApiKey))
            {
                Log.Error("Exception Replay agentless uploads enabled but DD_API_KEY is not set. Disabling Exception Replay.");
                throw new InvalidOperationException("Exception Replay agentless mode requires DD_API_KEY.");
            }

            if (!TryResolveAgentlessEndpoint(settings, out var baseUri, out var relativePath))
            {
                Log.Error("Exception Replay agentless uploads enabled but a valid intake URL could not be determined. Disabling Exception Replay.");
                throw new InvalidOperationException("Exception Replay agentless mode requires a valid intake URL.");
            }

            var apiFactory = DebuggerTransportStrategy.Get(baseUri);
            apiFactory = new HeaderInjectingApiRequestFactory(apiFactory, settings.AgentlessApiKey!);
            return new ExceptionReplayTransportInfo(apiFactory, null, relativePath, isAgentless: true);
        }

        private static ExceptionReplayTransportInfo CreateAgentTransport(TracerSettings tracerSettings, IDiscoveryService discoveryService)
        {
            var apiFactory = Agent.AgentTransportStrategy.Get(
                tracerSettings.Exporter,
                productName: "exception-replay",
                tcpTimeout: TimeSpan.FromSeconds(15),
                AgentHttpHeaderNames.MinimalHeaders,
                () => new MinimalAgentHeaderHelper(),
                uri => uri);

            return new ExceptionReplayTransportInfo(apiFactory, discoveryService, staticEndpoint: null, isAgentless: false);
        }

        private static bool TryResolveAgentlessEndpoint(ExceptionReplaySettings settings, out Uri baseUri, out string relativePath)
        {
            var overrideUrl = settings.AgentlessUrlOverride;
            if (string.IsNullOrWhiteSpace(overrideUrl))
            {
                baseUri = new Uri($"https://debugger-intake.{settings.AgentlessSite}/");
                relativePath = DefaultRelativePath;
                return true;
            }

            if (!Uri.TryCreate(overrideUrl, UriKind.Absolute, out var uri))
            {
                baseUri = null!;
                relativePath = string.Empty;
                return false;
            }

            baseUri = new Uri($"{uri.Scheme}://{uri.Authority}/");
            var path = uri.PathAndQuery;
            relativePath = string.IsNullOrEmpty(path) ? DefaultRelativePath : EnsureLeadingSlash(path);
            return true;
        }

        private static string EnsureLeadingSlash(string value)
        {
            return value.StartsWith("/", StringComparison.Ordinal) ? value : "/" + value;
        }

        private sealed class HeaderInjectingApiRequestFactory : IApiRequestFactory
        {
            private const string ApiKeyHeader = "DD-API-KEY";
            private const string RequestIdHeader = "DD-REQUEST-ID";
            private const string EvpOriginHeader = "DD-EVP-ORIGIN";
            private const string OriginValue = "dd-trace-dotnet";

            private readonly IApiRequestFactory _inner;
            private readonly string _apiKey;

            public HeaderInjectingApiRequestFactory(IApiRequestFactory inner, string apiKey)
            {
                _inner = inner;
                _apiKey = apiKey;
            }

            public string Info(Uri endpoint) => _inner.Info(endpoint);

            public Uri GetEndpoint(string relativePath) => _inner.GetEndpoint(relativePath);

            public IApiRequest Create(Uri endpoint)
            {
                var request = _inner.Create(endpoint);
                request.AddHeader(ApiKeyHeader, _apiKey);
                request.AddHeader(EvpOriginHeader, OriginValue);
                request.AddHeader(RequestIdHeader, Guid.NewGuid().ToString());
                return request;
            }

            public void SetProxy(System.Net.WebProxy proxy, System.Net.NetworkCredential credential) => _inner.SetProxy(proxy, credential);
        }
    }
}
