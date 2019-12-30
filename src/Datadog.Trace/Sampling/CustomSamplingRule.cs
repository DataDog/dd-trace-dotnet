using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Newtonsoft.Json;

namespace Datadog.Trace.Sampling
{
    internal class CustomSamplingRule : ISamplingRule
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<CustomSamplingRule>();
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private readonly float _samplingRate;
        private readonly string _serviceNameRegex;
        private readonly string _operationNameRegex;

        private bool _hasPoisonedRegex = false;

        public CustomSamplingRule(
            float rate,
            string ruleName,
            string serviceNameRegex,
            string operationNameRegex)
        {
            _samplingRate = rate;
            _serviceNameRegex = WrapWithLineCharacters(serviceNameRegex);
            _operationNameRegex = WrapWithLineCharacters(operationNameRegex);
            RuleName = ruleName;
        }

        public string RuleName { get; }

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
                Log.Error("Unable to parse custom sampling rules: {0}", ex);
            }

            return Enumerable.Empty<CustomSamplingRule>();
        }

        public bool IsMatch(Span span)
        {
            if (_hasPoisonedRegex)
            {
                return false;
            }

            if (DoesNotMatch(input: span.ServiceName, pattern: _serviceNameRegex))
            {
                return false;
            }

            if (DoesNotMatch(input: span.OperationName, pattern: _operationNameRegex))
            {
                return false;
            }

            return true;
        }

        public float GetSamplingRate(Span span)
        {
            span.SetMetric(Metrics.SamplingAgentDecision, _samplingRate);
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

        private bool DoesNotMatch(string input, string pattern)
        {
            try
            {
                if (pattern != null &&
                    !Regex.IsMatch(
                        input: input,
                        pattern: pattern,
                        options: RegexOptions.None,
                        matchTimeout: RegexTimeout))
                {
                    return true;
                }
            }
            catch (RegexMatchTimeoutException timeoutEx)
            {
                _hasPoisonedRegex = true;
                Log.Error(
                    timeoutEx,
                    "Timeout when trying to match against {0} on {1}.",
                    input,
                    pattern);
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
