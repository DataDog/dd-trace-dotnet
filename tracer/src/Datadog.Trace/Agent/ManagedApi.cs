// <copyright file="ManagedApi.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.Agent;

/// <summary>
/// A managed version of <see cref="Api"/> that is rebuilt whenever the exporter settings change
/// </summary>
internal sealed class ManagedApi : IApi
{
    private Api _api;

    public ManagedApi(
        TracerSettings.SettingsManager settings,
        IStatsdManager statsd,
        Action<Dictionary<string, float>> updateSampleRates,
        bool partialFlushEnabled)
    {
        UpdateApi(settings.InitialExporterSettings, settings.InitialMutableSettings.TracerMetricsEnabled);
        // ManagedApi lifetime matches application lifetime, so we don't bother to dispose the subscription
        settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                var mutable = changes.UpdatedMutable ?? changes.PreviousMutable;
                UpdateApi(exporter, mutable.TracerMetricsEnabled);
            }
            else if (changes.UpdatedMutable is { } mutable && mutable.TracerMetricsEnabled != changes.PreviousMutable.TracerMetricsEnabled)
            {
                _api.ToggleTracerHealthMetrics(mutable.TracerMetricsEnabled);
            }
        });

        [MemberNotNull(nameof(_api))]
        void UpdateApi(ExporterSettings exporterSettings, bool healthMetricsEnabled)
        {
            var apiRequestFactory = TracesTransportStrategy.Get(exporterSettings);
            var api = new Api(apiRequestFactory, statsd, ContainerMetadata.Instance, updateSampleRates, partialFlushEnabled, healthMetricsEnabled);
            Interlocked.Exchange(ref _api!, api);
        }
    }

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled = true)
        => Volatile.Read(ref _api).SendTracesAsync(traces, numberOfTraces, statsComputationEnabled, numberOfDroppedP0Traces, numberOfDroppedP0Spans, apmTracingEnabled);

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        => Volatile.Read(ref _api).SendStatsAsync(stats, bucketDuration);
}
