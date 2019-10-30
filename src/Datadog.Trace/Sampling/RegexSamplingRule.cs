using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Sampling
{
    internal class RegexSamplingRule : ISamplingRule
    {
        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.For<RegexSamplingRule>();
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

        private readonly float _samplingRate;
        private readonly string _serviceNameRegex;
        private readonly string _operationNameRegex;

        private bool _hasPoisonedRegex = false;

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

                        if (kvp.Length != 2 || string.IsNullOrWhiteSpace(kvp[0]) || string.IsNullOrWhiteSpace(kvp[1]))
                        {
                            Log.Warning("Rule {0} is malformed, skipping.", ruleName);
                            continue;
                        }

                        var key = kvp[0].Trim();
                        var value = kvp[1].Trim();

                        if (key.Equals("rate", StringComparison.OrdinalIgnoreCase) && float.TryParse(value, out rate))
                        {
                            if (rate < 0 || rate > 1)
                            {
                                // invalid rate
                                Log.Warning("Invalid rate {0} specified for sampling rule {1}, skipping.", rate, ruleName);
                                break;
                            }

                            rateSet = true;
                        }
                        else if (key.Equals("service", StringComparison.OrdinalIgnoreCase))
                        {
                            serviceNameRegex = WrapWithLineCharacters(value);
                        }
                        else if (key.Equals("operation", StringComparison.OrdinalIgnoreCase))
                        {
                            operationNameRegex = WrapWithLineCharacters(value);
                        }
                        else if (key.Equals("name", StringComparison.OrdinalIgnoreCase))
                        {
                            ruleName = value;
                        }
                    }

                    if (rateSet == false)
                    {
                        // Need a valid rate to be set to use a rule
                        Log.Warning("Rule {0} is missing the required rate, skipping.", ruleName);
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

        public float GetSamplingRate()
        {
            return _samplingRate;
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

        private static string WrapWithLineCharacters(string regex)
        {
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
    }
}
