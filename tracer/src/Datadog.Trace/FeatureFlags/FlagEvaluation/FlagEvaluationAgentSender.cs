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
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<FlagEvaluationAgentSender>();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly object _settingsLock = new();
    private IApiRequestFactory? _factory;

    internal FlagEvaluationAgentSender(ExporterSettings settings)
        : this(CreateFactory(settings))
    {
    }

    internal FlagEvaluationAgentSender(IApiRequestFactory factory)
    {
        _factory = factory;
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
                Log.Debug<int>("FeatureFlags flagevaluation Agent request failed with HTTP status {StatusCode}; dropping this batch without retry.", response.StatusCode);
            }
        }
        catch (Exception)
        {
            // Exception messages and response bodies can contain customer data.
            Log.Debug("FeatureFlags flagevaluation Agent request failed; dropping this batch without retry.");
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
