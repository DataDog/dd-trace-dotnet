﻿// <copyright file="RemoteCustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling;

internal class RemoteCustomSamplingRule : CustomSamplingRule
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RemoteCustomSamplingRule>();

    public RemoteCustomSamplingRule(
        float rate,
        string provenance,
        string? serviceNamePattern,
        string? operationNamePattern,
        string? resourceNamePattern,
        ICollection<KeyValuePair<string, string?>>? tagPatterns,
        TimeSpan timeout)
        : base(
            rate: rate,
            provenance: provenance,
            patternFormat: SamplingRulesFormat.Glob, // hard-coded, always "glob" for remote config rules
            serviceNamePattern: serviceNamePattern,
            operationNamePattern: operationNamePattern,
            resourceNamePattern: resourceNamePattern,
            tagPatterns: tagPatterns,
            timeout: timeout)
    {
    }

    public static IEnumerable<CustomSamplingRule> BuildFromConfigurationString(string configuration, TimeSpan timeout)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(configuration) &&
                JsonConvert.DeserializeObject<List<RemoteSamplingRuleJsonModel>>(configuration) is { Count: > 0 } rules)
            {
                var samplingRules = new List<CustomSamplingRule>(rules.Count);

                foreach (var r in rules)
                {
                    // "tags" has different json schema between local and remote config
                    var tags = ConvertToLocalTags(r.Tags);

                    var samplingRule = new RemoteCustomSamplingRule(
                        rate: r.SampleRate,
                        provenance: r.Provenance!, // never null for remote config rules
                        serviceNamePattern: r.Service,
                        operationNamePattern: r.OperationName,
                        resourceNamePattern: r.Resource,
                        tagPatterns: tags,
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

    /// <summary>
    /// Convert a list of tags in the remote configuration format ("tags": [{"key": "{key1}", "value_glob": "{value1}"}, ...])
    /// into the local configuration format ({"{key1}": "{value1}", ...}).
    /// </summary>
    internal static Dictionary<string, string?>? ConvertToLocalTags(List<RemoteSamplingRuleJsonModel.Tag>? remoteTags)
    {
        if (remoteTags == null)
        {
            return null;
        }

        var localTags = new Dictionary<string, string?>(remoteTags.Count);

        foreach (var tag in remoteTags)
        {
            if (tag is { Name: not null, Value: not null })
            {
                localTags[tag.Name] = tag.Value;
            }
        }

        return localTags;
    }

    internal class RemoteSamplingRuleJsonModel
    {
        [JsonRequired]
        [JsonProperty(PropertyName = "sample_rate")]
        public float SampleRate { get; set; }

        [JsonProperty(PropertyName = "provenance")]
        public string? Provenance { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string? OperationName { get; set; }

        [JsonProperty(PropertyName = "service")]
        public string? Service { get; set; }

        [JsonProperty(PropertyName = "resource")]
        public string? Resource { get; set; }

        [JsonProperty(PropertyName = "tags")]
        public List<Tag>? Tags { get; set; }

        internal class Tag
        {
            [JsonProperty(PropertyName = "key")]
            public string? Name { get; set; }

            [JsonProperty(PropertyName = "value_glob")]
            public string? Value { get; set; }
        }
    }
}
