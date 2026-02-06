// <copyright file="StatsdFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.Vendors.StatsdClient;
using Datadog.Trace.Vendors.StatsdClient.Transport;

namespace Datadog.Trace.DogStatsd;

internal static class StatsdFactory
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(StatsdFactory));

    internal static IDogStatsd CreateDogStatsdClient(
        MutableSettings settings,
        ExporterSettings exporter,
        bool includeDefaultTags,
        string? prefix = null)
    {
        var customTagCount = settings.GlobalTags.Count;
        var processTags = settings.ProcessTags?.TagsList;
        var tagsCount = (includeDefaultTags ? 5 + customTagCount : 0) + processTags?.Count ?? 0;
        var constantTags = new List<string>(tagsCount);
        if (includeDefaultTags)
        {
            constantTags.Add("lang:.NET");
            constantTags.Add($"lang_interpreter:{FrameworkDescription.Instance.Name}");
            constantTags.Add($"lang_version:{FrameworkDescription.Instance.ProductVersion}");
            constantTags.Add($"tracer_version:{TracerConstants.AssemblyVersion}");
            constantTags.Add($"{Tags.RuntimeId}:{Tracer.RuntimeId}");
            // update count above if adding new tags

            if (customTagCount > 0)
            {
                var tagProcessor = new TruncatorTagsProcessor();
                foreach (var kvp in settings.GlobalTags)
                {
                    var key = kvp.Key;
                    var value = kvp.Value;
                    tagProcessor.ProcessMeta(ref key, ref value);
                    constantTags.Add($"{key}:{value}");
                }
            }
        }

        if (processTags?.Count > 0)
        {
            constantTags.AddRange(processTags);
        }

        return CreateDogStatsdClient(settings, exporter, constantTags, prefix);
    }

    private static IDogStatsd CreateDogStatsdClient(
        MutableSettings settings,
        ExporterSettings exporter,
        List<string>? constantTags,
        string? prefix = null)
    {
        try
        {
            var statsd = new DogStatsdService();
            var config = new StatsdConfig
            {
                ConstantTags = constantTags is not null ? [..constantTags] : [],
                Prefix = prefix,
                // note that if these are null, statsd tries to grab them directly from the environment, which could be unsafe
                ServiceName = NormalizerTraceProcessor.NormalizeService(settings.DefaultServiceName),
                Environment = settings.Environment,
                ServiceVersion = settings.ServiceVersion,
                // Force flush interval to null to avoid ever sending telemetry, as these are recorded as custom metrics
                Advanced = { TelemetryFlushInterval = null },
            };

            switch (exporter.MetricsTransport)
            {
                case TransportType.NamedPipe:
                    config.PipeName = exporter.MetricsPipeName;
                    Log.Information("Using windows named pipes for metrics transport: {PipeName}", config.PipeName);
                    break;
#if NETCOREAPP3_1_OR_GREATER
                case TransportType.UDS:
                    config.StatsdServerName = $"{ExporterSettings.UnixDomainSocketPrefix}{exporter.MetricsUnixDomainSocketPath}";
                    Log.Information("Using unix domain sockets for metrics transport: {Socket}", config.StatsdServerName);
                    break;
#endif
                case TransportType.UDP:
                default:
                    config.StatsdServerName = exporter.MetricsHostname;
                    config.StatsdPort = exporter.DogStatsdPort;
                    Log.Information<string, int>("Using UDP for metrics transport: {Hostname}:{Port}", config.StatsdServerName, config.StatsdPort);
                    break;
            }

            statsd.Configure(config);
            return statsd;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to instantiate StatsD client");
            return new NoOpStatsd();
        }
    }
}
