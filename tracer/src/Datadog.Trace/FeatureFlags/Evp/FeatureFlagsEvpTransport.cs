// <copyright file="FeatureFlagsEvpTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Evp;

/// <summary>
/// Owns the historical fixed-v2 Feature Flags event sender independently of exposure batching.
/// </summary>
internal sealed class FeatureFlagsEvpTransport : IDisposable
{
    internal const string ExposureIntakePath = "api/v2/exposures";
    internal const string EventPlatformProxyV2 = "evp_proxy/v2";

    private readonly object _settingsLock = new();
    private readonly IDisposable? _settingsSubscription;
    private IApiRequestFactory _localRequestFactory;
    private int _disposed;

    internal FeatureFlagsEvpTransport(TracerSettings settings)
    {
        _localRequestFactory = CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);
        _settingsSubscription = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                lock (_settingsLock)
                {
                    if (_disposed == 0)
                    {
                        Interlocked.Exchange(ref _localRequestFactory, CreateLocalRequestFactory(exporter));
                    }
                }
            }
        });
    }

    [TestingOnly]
    internal FeatureFlagsEvpTransport(IApiRequestFactory localRequestFactory)
    {
        _localRequestFactory = localRequestFactory;
    }

    [TestingAndPrivateOnly]
    internal static IApiRequestFactory CreateLocalRequestFactory(ExporterSettings exporterSettings)
        => AgentTransportStrategy.Get(
            exporterSettings,
            productName: "FeatureFlags exposure",
            tcpTimeout: TimeSpan.FromSeconds(5),
            httpHeaderHelper: EventPlatformHeaderHelper.Instance);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _settingsSubscription?.Dispose();
        }
    }

    internal async Task SendAsync<T>(T payload, string intakePath, JsonSerializerSettings serializerSettings)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var factory = Volatile.Read(ref _localRequestFactory);
        var request = factory.Create(factory.GetEndpoint($"{EventPlatformProxyV2}/{intakePath}"));
        using var response = await request.PostAsJsonAsync(payload, MultipartCompression.GZip, serializerSettings).ConfigureAwait(false);
    }
}
