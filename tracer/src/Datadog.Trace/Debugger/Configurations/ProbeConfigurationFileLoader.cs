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
using Datadog.Trace.Vendors.Newtonsoft.Json;
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

            using var reader = new StreamReader(probeFile);
            using var jsonReader = new JsonTextReader(reader) { ArrayPool = JsonArrayPool.Shared };
            if (!await jsonReader.ReadAsync().ConfigureAwait(false))
            {
                Log.Debug("Probe file is empty: {ProbeFile}", probeFile);
                return null;
            }

            var jArray = await JArray.LoadAsync(jsonReader).ConfigureAwait(false);
            while (await jsonReader.ReadAsync().ConfigureAwait(false))
            {
                // Validate no trailing content after the array.
            }

            List<LogProbe>? logs = null;
            List<MetricProbe>? metrics = null;
            List<SpanProbe>? spans = null;
            List<SpanDecorationProbe>? spanDecorations = null;
            var serializer = JsonSerializer.CreateDefault();
            var duplicateProbes = 0;

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
                            var logProbe = jObject.ToObject<LogProbe>(serializer);
                            if (logProbe is not null && IsValidProbe(logProbe))
                            {
                                if (!AddProbe(ref logs, logProbe))
                                {
                                    duplicateProbes++;
                                }
                            }

                            break;
                        case "METRIC_PROBE":
                            var metricProbe = jObject.ToObject<MetricProbe>(serializer);
                            if (metricProbe is not null && IsValidProbe(metricProbe))
                            {
                                if (!AddProbe(ref metrics, metricProbe))
                                {
                                    duplicateProbes++;
                                }
                            }

                            break;
                        case "SPAN_PROBE":
                            var spanProbe = jObject.ToObject<SpanProbe>(serializer);
                            if (spanProbe is not null && IsValidProbe(spanProbe))
                            {
                                if (!AddProbe(ref spans, spanProbe))
                                {
                                    duplicateProbes++;
                                }
                            }

                            break;
                        case "SPAN_DECORATION_PROBE":
                            var spanDecorationProbe = jObject.ToObject<SpanDecorationProbe>(serializer);
                            if (spanDecorationProbe is not null && IsValidProbe(spanDecorationProbe))
                            {
                                if (!AddProbe(ref spanDecorations, spanDecorationProbe))
                                {
                                    duplicateProbes++;
                                }
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

            var uniqueCount = (logs?.Count ?? 0) + (metrics?.Count ?? 0) + (spans?.Count ?? 0) + (spanDecorations?.Count ?? 0);
            if (uniqueCount == 0)
            {
                Log.Warning("No valid probes found in file: {ProbeFile}", probeFile);
                return null;
            }

            if (duplicateProbes != 0)
            {
                Log.Debug("Removed {Count} duplicate probe(s) from file", property: duplicateProbes);
            }

            Log.Information("Successfully loaded {Count} probes from file.", property: uniqueCount);

            return new ProbeConfiguration
            {
                LogProbes = ToArrayOrEmpty(logs),
                MetricProbes = ToArrayOrEmpty(metrics),
                SpanProbes = ToArrayOrEmpty(spans),
                SpanDecorationProbes = ToArrayOrEmpty(spanDecorations)
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

    private static bool AddProbe<T>(ref List<T>? probes, T probe)
        where T : ProbeDefinition
    {
        if (probes is not null)
        {
            for (var i = 0; i < probes.Count; i++)
            {
                if (probes[i].Id == probe.Id)
                {
                    Log.Warning("Duplicate probe ID '{Id}' found in file, using first occurrence", probe.Id);
                    return false;
                }
            }
        }

        (probes ??= new()).Add(probe);
        return true;
    }

    private static T[] ToArrayOrEmpty<T>(List<T>? probes)
    {
        return probes?.ToArray() ?? [];
    }
}
