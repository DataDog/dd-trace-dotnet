using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RegexSamplingRule : ISamplingRule
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RegexSamplingRule>();

        private readonly float _samplingRate;
        private readonly string _serviceNameRegex;
        private readonly string _operationNameRegex;

        public RegexSamplingRule(
            float rate,
            string name,
            string serviceNameRegex,
            string operationNameRegex)
        {
            _samplingRate = rate;
            _serviceNameRegex = serviceNameRegex;
            _operationNameRegex = operationNameRegex;

            Name = name;
        }

        public string Name { get; }

        /// <summary>
        /// Gets the Priority of the rule.
        /// Configuration rules will default to 1 as a priority and rely on order of specification.
        /// </summary>
        public int Priority => 1;

        public static IEnumerable<RegexSamplingRule> BuildFromConfigurationString(string configuration)
        {
            if (!string.IsNullOrEmpty(configuration))
            {
                var ruleStrings = configuration.Split(new[] { ";", ":" }, StringSplitOptions.RemoveEmptyEntries);
                var index = 0;

                foreach (var ruleString in ruleStrings)
                {
                    index++;

                    var ruleParts = ruleString.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    var rateSet = false;
                    float rate = 0;
                    string serviceNameRegex = null, operationNameRegex = null, ruleName = $"config_rule_{index}";

                    foreach (var rulePart in ruleParts)
                    {
                        var kvp = rulePart.Split(new[] { "=" }, StringSplitOptions.None);

                        if (kvp.Length != 2 || string.IsNullOrWhiteSpace(kvp[1]))
                        {
                            Log.Warning("Rule {0} is malformed, skipping.",  ruleName);
                            continue;
                        }

                        var key = kvp[0];
                        var value = kvp[1];

                        if (key.ToLower() == "rate" && float.TryParse(value, out rate))
                        {
                            if (rate < 0 || rate > 1)
                            {
                                // invalid rate
                                Log.Warning("Invalid rate {0} specified for sampling rule {1}, skipping.", rate, ruleName);
                                break;
                            }

                            rateSet = true;
                        }
                        else if (key == "service")
                        {
                            serviceNameRegex = value;
                        }
                        else if (key == "operation")
                        {
                            operationNameRegex = value;
                        }
                        else if (key == "name")
                        {
                            ruleName = value;
                        }
                    }

                    if (rateSet == false)
                    {
                        // Need a valid rate to be set to use a rule
                        continue;
                    }

                    yield return new RegexSamplingRule(
                        rate: rate,
                        name: ruleName,
                        serviceNameRegex: serviceNameRegex,
                        operationNameRegex: operationNameRegex);
                }
            }
        }

        public bool IsMatch(Span span)
        {
            if (_serviceNameRegex != null && !Regex.IsMatch(input: span.ServiceName, pattern: _serviceNameRegex))
            {
                return false;
            }

            if (_operationNameRegex != null && !Regex.IsMatch(input: span.OperationName, pattern: _operationNameRegex))
            {
                return false;
            }

            return true;
        }

        public float GetSamplingRate(Span span)
        {
            return _samplingRate;
        }
    }
}
