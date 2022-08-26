// <copyright file="CustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling
{
    internal class CustomSamplingRule : ISamplingRule
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CustomSamplingRule>();
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private readonly float _samplingRate;
        private readonly Regex _serviceNameRegex;
        private readonly Regex _operationNameRegex;

        private bool _hasPoisonedRegex;

        public CustomSamplingRule(
            float rate,
            string ruleName,
            string serviceNameRegex,
            string operationNameRegex)
        {
            _samplingRate = rate;

            _serviceNameRegex = serviceNameRegex is null
                                    ? null
                                    : new(
                                        WrapWithLineCharacters(serviceNameRegex),
                                        RegexOptions.Compiled,
                                        RegexTimeout);
            _operationNameRegex = operationNameRegex is null
                                      ? null
                                      : new(
                                          WrapWithLineCharacters(operationNameRegex),
                                          RegexOptions.Compiled,
                                          RegexTimeout);

            RuleName = ruleName;
        }

        public string RuleName { get; }

        public int SamplingMechanism => Datadog.Trace.Sampling.SamplingMechanism.TraceSamplingRule;

        /// <summary>
        /// Gets or sets the priority of the rule.
        /// Configuration rules will default to 1 as a priority and rely on order of specification.
        /// </summary>
        public int Priority { get; protected set; } = 1;

        public static IEnumerable<CustomSamplingRule> BuildFromConfigurationString(string configuration)
        {
            try
            {
                if (!string.IsNullOrEmpty(configuration))
                {
                    var index = 0;
                    var rules = JsonConvert.DeserializeObject<List<CustomRuleConfig>>(configuration);
                    return rules.Select(
                        r =>
                        {
                            index++; // Used to create a readable rule name if one is not specified
                            return new CustomSamplingRule(r.SampleRate, r.RuleName ?? $"config-rule-{index}", r.Service, r.OperationName);
                        });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to parse custom sampling rules");
            }

            return Enumerable.Empty<CustomSamplingRule>();
        }

        public bool IsMatch(Span span)
        {
            if (_hasPoisonedRegex)
            {
                return false;
            }

            if (DoesNotMatch(input: span.ServiceName, regex: _serviceNameRegex))
            {
                return false;
            }

            if (DoesNotMatch(input: span.OperationName, regex: _operationNameRegex))
            {
                return false;
            }

            return true;
        }

        public float GetSamplingRate(Span span)
        {
            span.SetMetric(Metrics.SamplingRuleDecision, _samplingRate);
            return _samplingRate;
        }

        private static string WrapWithLineCharacters(string regex)
        {
            if (regex == null)
            {
                return regex;
            }

            if (!regex.StartsWith("^"))
            {
                regex = "^" + regex;
            }

            if (!regex.EndsWith("$"))
            {
                regex = regex + "$";
            }

            return regex;
        }

        private bool DoesNotMatch(string input, Regex regex)
        {
            try
            {
                if (regex is not null)
                {
                    if (!regex.Match(input).Success)
                    {
                        return true;
                    }
                }
            }
            catch (RegexMatchTimeoutException timeoutEx)
            {
                _hasPoisonedRegex = true;
                Log.Error(
                    timeoutEx,
                    "Timeout when trying to match against {Value} on {Pattern}.",
                    input,
                    regex?.ToString());
            }

            return false;
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
