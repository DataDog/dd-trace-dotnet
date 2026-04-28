// <copyright file="ProbeConfigurationFileLoader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Debugger.Configurations;

internal static class ProbeConfigurationFileLoader
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeConfigurationFileLoader));

    public static async Task<ProbeConfiguration?> LoadAsync(string? probeFile)
    {
        if (string.IsNullOrEmpty(probeFile))
        {
            return null;
        }

        try
        {
            if (!File.Exists(probeFile))
            {
                Log.Warning("Probe file specified but not found: {ProbeFile}", probeFile);
                return null;
            }

            Log.Information("Loading probes from file: {ProbeFile}", probeFile);

            string fileContent;
            using (var reader = new StreamReader(probeFile))
            {
                fileContent = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                Log.Debug("Probe file is empty: {ProbeFile}", probeFile);
                return null;
            }

            var jArray = JsonHelper.ParseJArray(fileContent);
            var logs = new List<LogProbe>();
            var metrics = new List<MetricProbe>();
            var spans = new List<SpanProbe>();
            var spanDecorations = new List<SpanDecorationProbe>();

            foreach (var jToken in jArray)
            {
                var jObject = jToken as JObject;
                if (jObject == null)
                {
                    Log.Warning("Invalid probe entry in file, skipping");
                    continue;
                }

                var typeToken = jObject["type"];
                if (typeToken == null)
                {
                    Log.Warning("Probe entry missing 'type' field, skipping");
                    continue;
                }

                var type = typeToken.ToString();
                try
                {
                    switch (type)
                    {
                        case "LOG_PROBE":
                            var logProbe = jObject.ToObject<LogProbe>();
                            if (logProbe is not null && IsValidProbe(logProbe))
                            {
                                logs.Add(logProbe);
                            }

                            break;
                        case "METRIC_PROBE":
                            var metricProbe = jObject.ToObject<MetricProbe>();
                            if (metricProbe is not null && IsValidProbe(metricProbe))
                            {
                                metrics.Add(metricProbe);
                            }

                            break;
                        case "SPAN_PROBE":
                            var spanProbe = jObject.ToObject<SpanProbe>();
                            if (spanProbe is not null && IsValidProbe(spanProbe))
                            {
                                spans.Add(spanProbe);
                            }

                            break;
                        case "SPAN_DECORATION_PROBE":
                            var spanDecorationProbe = jObject.ToObject<SpanDecorationProbe>();
                            if (spanDecorationProbe is not null && IsValidProbe(spanDecorationProbe))
                            {
                                spanDecorations.Add(spanDecorationProbe);
                            }

                            break;
                        default:
                            Log.Warning("Unknown probe type '{Type}' in file, skipping", type);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to deserialize probe of type '{Type}', skipping", type);
                }
            }

            var totalProbes = logs.Count + metrics.Count + spans.Count + spanDecorations.Count;
            if (totalProbes == 0)
            {
                Log.Warning("No valid probes found in file: {ProbeFile}", probeFile);
                return null;
            }

            var uniqueLogs = DeduplicateProbes(logs);
            var uniqueMetrics = DeduplicateProbes(metrics);
            var uniqueSpans = DeduplicateProbes(spans);
            var uniqueSpanDecorations = DeduplicateProbes(spanDecorations);

            var uniqueCount = uniqueLogs.Length + uniqueMetrics.Length + uniqueSpans.Length + uniqueSpanDecorations.Length;
            if (uniqueCount < totalProbes)
            {
                Log.Debug("Removed {Count} duplicate probe(s) from file", property: totalProbes - uniqueCount);
            }

            Log.Information("Successfully loaded {Count} probes from file.", property: uniqueCount);

            return new ProbeConfiguration
            {
                LogProbes = uniqueLogs,
                MetricProbes = uniqueMetrics,
                SpanProbes = uniqueSpans,
                SpanDecorationProbes = uniqueSpanDecorations
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load probes from file: {ProbeFile}", probeFile);
            return null;
        }
    }

    private static bool IsValidProbe(ProbeDefinition probe)
    {
        if (StringUtil.IsNullOrEmpty(probe.Id))
        {
            Log.Warning("Probe entry missing 'id' field, skipping");
            return false;
        }

        return true;
    }

    private static T[] DeduplicateProbes<T>(List<T> probes)
        where T : ProbeDefinition
    {
        if (probes.Count == 0)
        {
            return [];
        }

        var uniqueProbes = new List<T>(probes.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var probe in probes)
        {
            if (!seenIds.Add(probe.Id))
            {
                Log.Warning("Duplicate probe ID '{Id}' found in file, using first occurrence", probe.Id);
                continue;
            }

            uniqueProbes.Add(probe);
        }

        return uniqueProbes.ToArray();
    }
}
