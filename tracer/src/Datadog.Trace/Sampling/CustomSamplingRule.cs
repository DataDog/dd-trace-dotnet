// <copyright file="CustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Vendors.IndieSystem.Text.RegularExpressions;
#else
using System.Text.RegularExpressions;
#endif
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
        private readonly Regex _serviceNameRegex;
        private readonly Regex _operationNameRegex;

        private bool _regexTimedOut;

        public CustomSamplingRule(
            float rate,
            string ruleName,
            string patternFormat,
            string serviceNameRegex,
            string operationNameRegex)
        {
            _samplingRate = rate;
            RuleName = ruleName;

            _serviceNameRegex = RegexBuilder.Build(serviceNameRegex, patternFormat);
            _operationNameRegex = RegexBuilder.Build(operationNameRegex, patternFormat);

            if (_serviceNameRegex is null &&
                _operationNameRegex is null)
            {
                // if no patterns were specified, this rule always matches (i.e. catch-all)
                _alwaysMatch = true;
            }
        }

        public string RuleName { get; }

        public int SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.TraceSamplingRule;

        /// <summary>
        /// Gets or sets the priority of the rule.
        /// Configuration rules will default to 1 as a priority and rely on order of specification.
        /// </summary>
        public int Priority { get; protected set; } = 1;

        public static IEnumerable<CustomSamplingRule> BuildFromConfigurationString(string configuration, string patternFormat)
        {
            if (string.IsNullOrWhiteSpace(configuration))
            {
                return Enumerable.Empty<CustomSamplingRule>();
            }

            try
            {
                var rules = JsonConvert.DeserializeObject<List<CustomRuleConfig>>(configuration);

                if (rules == null)
                {
                    return Enumerable.Empty<CustomSamplingRule>();
                }

                var index = 0;
                var samplingRules = new List<CustomSamplingRule>(rules.Count);

                foreach (var r in rules)
                {
                    index++; // Used to create a readable rule name if one is not specified

                    samplingRules.Add(
                        new CustomSamplingRule(
                            r.SampleRate,
                            r.RuleName ?? $"config-rule-{index}",
                            patternFormat,
                            r.Service,
                            r.OperationName));
                }

                return samplingRules;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to parse the trace sampling rules.");
                return Enumerable.Empty<CustomSamplingRule>();
            }
        }

        public bool IsMatch(Span span)
        {
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

            try
            {
                // if a regex is null (not specified), it always matches.
                // stop as soon as we find a non-match.
                return (_serviceNameRegex?.Match(span.ServiceName).Success ?? true) &&
                       (_operationNameRegex?.Match(span.OperationName).Success ?? true);
            }
            catch (RegexMatchTimeoutException e)
            {
                // flag regex so we don't try to use it again
                _regexTimedOut = true;

                Log.Error(
                    e,
                    """Regex timed out when trying to match value "{Input}" against pattern "{Pattern}".""",
                    e.Input,
                    e.Pattern);

                return false;
            }
        }

        public float GetSamplingRate(Span span)
        {
            span.SetMetric(Metrics.SamplingRuleDecision, _samplingRate);
            return _samplingRate;
        }

        [Serializable]
        private class CustomRuleConfig
        {
            [JsonProperty(PropertyName = "rule_name")]
            public string RuleName { get; set; }

            [JsonRequired]
            [JsonProperty(PropertyName = "sample_rate")]
            public float SampleRate { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string OperationName { get; set; }

            [JsonProperty(PropertyName = "service")]
            public string Service { get; set; }
        }
    }
}
