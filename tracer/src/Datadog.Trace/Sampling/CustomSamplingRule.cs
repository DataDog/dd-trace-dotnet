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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling
{
    internal class CustomSamplingRule : ISamplingRule
    {
#if NETCOREAPP3_1_OR_GREATER
        private const RegexOptions DefaultRegexOptions = RegexOptions.Compiled | RegexOptions.NonBacktracking;
#else
        private const RegexOptions DefaultRegexOptions = RegexOptions.Compiled;
#endif

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<CustomSamplingRule>();
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

        private readonly float _samplingRate;
        private readonly Regex _serviceNameRegex;
        private readonly Regex _operationNameRegex;

        private bool _hasPoisonedRegex;

        public CustomSamplingRule(
            float rate,
            string ruleName,
            string patternFormat,
            string serviceNameRegex,
            string operationNameRegex)
        {
            _samplingRate = rate;
            RuleName = ruleName;

            _serviceNameRegex = BuildRegex(serviceNameRegex, patternFormat);
            _operationNameRegex = BuildRegex(operationNameRegex, patternFormat);
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
            try
            {
                if (!string.IsNullOrEmpty(configuration))
                {
                    var index = 0;
                    var rules = JsonConvert.DeserializeObject<List<CustomRuleConfig>>(configuration);
                    var samplingRules = new List<CustomSamplingRule>();

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

            // special case where if both regex are null we match
            if (_serviceNameRegex is null && _operationNameRegex is null)
            {
                return true;
            }

            try
            {
                // if the regex is null, we will always match it as we don't care
                var serviceMatch = _serviceNameRegex?.Match(span.ServiceName).Success ?? true;
                var operationMatch = _operationNameRegex?.Match(span.OperationName).Success ?? true;
                return serviceMatch && operationMatch;
            }
            catch (RegexMatchTimeoutException e)
            {
                _hasPoisonedRegex = true;
                Log.Error(
                    e,
                    "Timeout when trying to match against {Value} on {Pattern}.",
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

        private static string WrapWithLineCharacters(string regex)
        {
            if (regex == null)
            {
                return null;
            }

            var sb = StringBuilderCache.Acquire(regex.Length + 2);

            if (!regex.StartsWith("^"))
            {
                sb.Append('^');
            }

            sb.Append(regex);

            if (!regex.EndsWith("$"))
            {
                sb.Append('$');
            }

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        private static Regex BuildRegex(string pattern, string patternFormat)
        {
            try
            {
                if (pattern is null)
                {
                    return null;
                }

                switch (patternFormat)
                {
                    case CustomSamplingRulesFormat.Regex:
                        return new Regex(WrapWithLineCharacters(pattern), DefaultRegexOptions, RegexTimeout);

                    case CustomSamplingRulesFormat.Glob:
                        return GlobMatcher.BuildRegex(pattern, DefaultRegexOptions, RegexTimeout);

                    default:
                        // ReSharper disable once RedundantNameQualifier (only redundant for some target frameworks)
                        Util.ThrowHelper.ThrowArgumentOutOfRangeException(nameof(patternFormat), patternFormat, "Invalid match pattern format. Valid values are 'regex' or 'glob'.");
                        return null; // unreachable
                }
            }
            catch (ArgumentException e)
            {
                Log.Error(e, "Invalid {Format} match pattern: {Pattern}", patternFormat, pattern);
                throw;
            }
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
