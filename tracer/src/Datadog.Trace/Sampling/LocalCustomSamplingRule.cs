// <copyright file="LocalCustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling;

internal class LocalCustomSamplingRule : CustomSamplingRule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LocalCustomSamplingRule>();

    public LocalCustomSamplingRule(
        float rate,
        string patternFormat,
        string? serviceNamePattern,
        string? operationNamePattern,
        string? resourceNamePattern,
        ICollection<KeyValuePair<string, string?>>? tagPatterns,
        TimeSpan timeout)
        : base(
            rate: rate,
            provenance: SamplingRuleProvenance.Local, // hard-coded, not present in local config json
            patternFormat: patternFormat,
            serviceNamePattern: serviceNamePattern,
            operationNamePattern: operationNamePattern,
            resourceNamePattern: resourceNamePattern,
            tagPatterns: tagPatterns,
            timeout: timeout)
    {
    }

    public static IEnumerable<CustomSamplingRule> BuildFromConfigurationString(
        string configuration,
        string patternFormat,
        TimeSpan timeout)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(configuration) &&
                JsonConvert.DeserializeObject<List<LocalSamplingRuleJsonModel>>(configuration) is { Count: > 0 } rules)
            {
                var samplingRules = new List<CustomSamplingRule>(rules.Count);

                foreach (var r in rules)
                {
                    var samplingRule = new LocalCustomSamplingRule(
                        rate: r.SampleRate,
                        patternFormat: patternFormat, // from DD_TRACE_SAMPLING_RULES_FORMAT
                        serviceNamePattern: r.Service,
                        operationNamePattern: r.OperationName,
                        resourceNamePattern: r.Resource,
                        tagPatterns: r.Tags,
                        timeout: timeout);

                    samplingRules.Add(samplingRule);
                }

                return samplingRules;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unable to parse the trace sampling rules.");
        }

        return [];
    }

    internal class LocalSamplingRuleJsonModel
    {
        [JsonRequired]
        [JsonProperty(PropertyName = "sample_rate")]
        public float SampleRate { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string? OperationName { get; set; }

        [JsonProperty(PropertyName = "service")]
        public string? Service { get; set; }

        [JsonProperty(PropertyName = "resource")]
        public string? Resource { get; set; }

        [JsonProperty(PropertyName = "tags")]
        public Dictionary<string, string?>? Tags { get; set; }
    }
}
