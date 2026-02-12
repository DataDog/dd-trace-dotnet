// <copyright file="ManagedApiOtlp.cs" company="Datadog">
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

namespace Datadog.Trace.Agent;

/// <summary>
/// A managed version of <see cref="Api"/> that is rebuilt whenever the exporter settings change
/// </summary>
internal sealed class ManagedApiOtlp : IApi
{
    private IApi _api;

    public ManagedApiOtlp(TracerSettings.SettingsManager settings)
    {
        UpdateApi(settings.InitialExporterSettings);
        // ManagedApi lifetime matches application lifetime, so we don't bother to dispose the subscription
        settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                var mutable = changes.UpdatedMutable ?? changes.PreviousMutable;
                UpdateApi(exporter);
            }
        });

        [MemberNotNull(nameof(_api))]
        void UpdateApi(ExporterSettings exporterSettings)
        {
            var apiRequestFactory = TracesTransportStrategy.Get(exporterSettings);
            var api = new ApiOtlp(apiRequestFactory, exporterSettings.OtlpTracesEndpoint ?? new Uri("http://localhost:4318/v1/traces"), exporterSettings.TracesEncoding, exporterSettings.OtlpMetricsProtocol ?? OtlpProtocol.HttpProtobuf, exporterSettings.OtlpMetricsEndpoint, exporterSettings.OtlpMetricsHeaders, exporterSettings.OtlpMetricsTimeoutMs);
            Interlocked.Exchange(ref _api!, api);
        }
    }

    public TracesEncoding TracesEncoding => Volatile.Read(ref _api).TracesEncoding;

    public Task<bool> Ping() => Volatile.Read(ref _api).Ping();

    public Task<bool> SendTracesAsync(ArraySegment<byte> traces, int numberOfTraces, bool statsComputationEnabled, long numberOfDroppedP0Traces, long numberOfDroppedP0Spans, bool apmTracingEnabled = true)
        => Volatile.Read(ref _api).SendTracesAsync(traces, numberOfTraces, statsComputationEnabled, numberOfDroppedP0Traces, numberOfDroppedP0Spans, apmTracingEnabled);

    public Task<bool> SendStatsAsync(StatsBuffer stats, long bucketDuration)
        => Volatile.Read(ref _api).SendStatsAsync(stats, bucketDuration);
}
