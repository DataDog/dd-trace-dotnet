// <copyright file="FlagEvaluationAgentSender.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

internal sealed class FlagEvaluationAgentSender : IDisposable
{
    private static readonly IDatadogLogger StaticLog = DatadogLogging.GetLoggerFor<FlagEvaluationAgentSender>();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly IDatadogLogger _log;
    private readonly object _settingsLock = new();
    private IApiRequestFactory? _factory;

    internal FlagEvaluationAgentSender(ExporterSettings settings)
        : this(CreateFactory(settings))
    {
    }

    internal FlagEvaluationAgentSender(IApiRequestFactory factory, IDatadogLogger? log = null)
    {
        _factory = factory;
        _log = log ?? StaticLog;
    }

    internal void UpdateExporterSettings(ExporterSettings settings)
    {
        lock (_settingsLock)
        {
            if (_factory is not null)
            {
                Volatile.Write(ref _factory, CreateFactory(settings));
            }
        }
    }

    internal async Task SendCompressedAsync(ArraySegment<byte> payload)
    {
        // A settings update or close cannot change the factory of an admitted request.
        var factory = Volatile.Read(ref _factory);
        if (factory is null)
        {
            return;
        }

        try
        {
            var request = factory.Create(factory.GetEndpoint("evp_proxy/v2/api/v2/flagevaluation"));
            using var response = await request.PostAsync(payload, "application/json", "gzip").ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300)
            {
                // Payload/client rejections need error telemetry. Missing Agent routes, timeouts,
                // throttling and server failures are environmental, but still visible locally.
                if (response.StatusCode is >= 400 and < 500 and not (404 or 405 or 408 or 429))
                {
                    _log.Error<int>("FeatureFlags flagevaluation Agent request rejected with HTTP status {StatusCode}; dropping this batch without retry.", response.StatusCode);
                }
                else
                {
                    _log.ErrorSkipTelemetry<int>("FeatureFlags flagevaluation Agent request to evp_proxy/v2/api/v2/flagevaluation failed with HTTP status {StatusCode} after 1 attempt; dropping this batch without retry. See https://docs.datadoghq.com/tracing/troubleshooting/connection_errors/?code-lang=dotnet", response.StatusCode);
                }
            }
        }
        catch (Exception)
        {
            // Exception messages and response bodies can contain customer data.
            _log.ErrorSkipTelemetry("FeatureFlags flagevaluation Agent request to evp_proxy/v2/api/v2/flagevaluation failed after 1 attempt; dropping this batch without retry. See https://docs.datadoghq.com/tracing/troubleshooting/connection_errors/?code-lang=dotnet");
        }
    }

    public void Dispose()
    {
        lock (_settingsLock)
        {
            // Existing factories have no disposal contract. Let in-flight requests finish;
            // clearing our reference closes admission without retaining the last factory.
            Volatile.Write(ref _factory, null);
        }
    }

    private static IApiRequestFactory CreateFactory(ExporterSettings settings) => AgentTransportStrategy.Get(
        settings,
        productName: "FeatureFlags flagevaluation",
        tcpTimeout: RequestTimeout,
        httpHeaderHelper: FlagEvaluationAgentHeaderHelper.Instance,
        requestTimeout: RequestTimeout);
}
