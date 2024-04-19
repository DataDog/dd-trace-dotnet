// <copyright file="CustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling
{
    internal class CustomSamplingRule : ISamplingRule
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CustomSamplingRule>();

        private readonly float _samplingRate;
        private readonly bool _alwaysMatch;

        // TODO consider moving toward these https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Text/SimpleRegex.cs
        private readonly Regex? _serviceNameRegex;
        private readonly Regex? _operationNameRegex;
        private readonly Regex? _resourceNameRegex;
        private readonly List<KeyValuePair<string, Regex?>>? _tagRegexes;

        private bool _regexTimedOut;

        public CustomSamplingRule(
            float rate,
            string patternFormat,
            string? serviceNamePattern,
            string? operationNamePattern,
            string? resourceNamePattern,
            ICollection<KeyValuePair<string, string?>>? tagPatterns,
            TimeSpan timeout)
        {
            _samplingRate = rate;

            _serviceNameRegex = RegexBuilder.Build(serviceNamePattern, patternFormat, timeout);
            _operationNameRegex = RegexBuilder.Build(operationNamePattern, patternFormat, timeout);
            _resourceNameRegex = RegexBuilder.Build(resourceNamePattern, patternFormat, timeout);
            _tagRegexes = RegexBuilder.Build(tagPatterns, patternFormat, timeout);

            if (_serviceNameRegex is null &&
                _operationNameRegex is null &&
                _resourceNameRegex is null &&
                (_tagRegexes is null || _tagRegexes.Count == 0))
            {
                // if no patterns were specified, this rule always matches (i.e. catch-all)
                _alwaysMatch = true;
            }
        }

        public int SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.TraceSamplingRule;

        /// <summary>
        /// Gets the priority of the rule.
        /// Configuration rules will default to 1 as a priority and rely on order of specification.
        /// </summary>
        public int Priority => 1;

        public static IEnumerable<CustomSamplingRule> BuildFromConfigurationString(string configuration, string patternFormat, TimeSpan timeout)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(configuration) &&
                    JsonConvert.DeserializeObject<List<CustomRuleConfig>>(configuration) is { Count: > 0 } rules)
                {
                    var samplingRules = new List<CustomSamplingRule>(rules.Count);

                    foreach (var r in rules)
                    {
                        samplingRules.Add(
                            new CustomSamplingRule(
                                rate: r.SampleRate,
                                patternFormat: patternFormat,
                                serviceNamePattern: r.Service,
                                operationNamePattern: r.OperationName,
                                resourceNamePattern: r.Resource,
                                tagPatterns: r.Tags,
                                timeout: timeout));
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

        public bool IsMatch(Span span)
        {
            if (span == null!)
            {
                return false;
            }

            if (_alwaysMatch)
            {
                // the rule is a catch-all
                return true;
            }

            if (_regexTimedOut)
            {
                // the regex had a valid format, but it timed out previously. stop trying to use it.
                return false;
            }

            return SamplingRuleHelper.IsMatch(
                span,
                serviceNameRegex: _serviceNameRegex,
                operationNameRegex: _operationNameRegex,
                resourceNameRegex: _resourceNameRegex,
                tagRegexes: _tagRegexes,
                out _regexTimedOut);
        }

        public float GetSamplingRate(Span span)
        {
            span.SetMetric(Metrics.SamplingRuleDecision, _samplingRate);
            return _samplingRate;
        }

        public override string ToString()
        {
            // later this will return different values depending on the rule's provenance:
            // local, customer (remote), or dynamic (remote)
            return "LocalSamplingRule";
        }

        // The [Serializable] attribute is not here because we use the BinaryFormatter.
        // It is here to silence certain compiler warnings about the
        // class not being instantiated or property setters not used.
        [Serializable]
        private class CustomRuleConfig
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
}
