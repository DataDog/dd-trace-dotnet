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

internal sealed class LocalCustomSamplingRule : CustomSamplingRule
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
            patternFormat: patternFormat,
            serviceNamePattern: serviceNamePattern,
            operationNamePattern: operationNamePattern,
            resourceNamePattern: resourceNamePattern,
            tagPatterns: tagPatterns,
            timeout: timeout)
    {
    }

    public override string Provenance => SamplingRuleProvenance.Local;

    public override int SamplingMechanism => Sampling.SamplingMechanism.LocalTraceSamplingRule;

    public static LocalCustomSamplingRule[] BuildFromConfigurationString(
        string configuration,
        string patternFormat,
        TimeSpan timeout)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(configuration) &&
                JsonConvert.DeserializeObject<List<RuleConfigJsonModel>>(configuration) is { Count: > 0 } ruleModels)
            {
                var samplingRules = new LocalCustomSamplingRule[ruleModels.Count];

                for (var i = 0; i < ruleModels.Count; i++)
                {
                    var r = ruleModels[i];

                    var samplingRule = new LocalCustomSamplingRule(
                        rate: r.SampleRate,
                        patternFormat: patternFormat, // from DD_TRACE_SAMPLING_RULES_FORMAT
                        serviceNamePattern: r.Service,
                        operationNamePattern: r.OperationName,
                        resourceNamePattern: r.Resource,
                        tagPatterns: r.Tags,
                        timeout: timeout);

                    samplingRules[i] = samplingRule;
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

    internal class RuleConfigJsonModel
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
