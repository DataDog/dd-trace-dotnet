// <copyright file="SpanSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Sampling
{
    /// <summary>
    /// Represents a sampling rules for single span ingestion.
    /// </summary>
    internal class SpanSamplingRule : ISpanSamplingRule
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<SpanSamplingRule>();

        private readonly bool _alwaysMatch;

        // TODO consider moving toward this https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Text/SimpleRegex.cs
        private readonly Regex _serviceNameRegex;
        private readonly Regex _operationNameRegex;
        private readonly Regex _resourceNameRegex;
        private readonly List<KeyValuePair<string, Regex>> _tagRegexes;

        private readonly IRateLimiter _limiter;
        private bool _regexTimedOut;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanSamplingRule"/> class.
        /// </summary>
        /// <param name="serviceNameGlob">The glob pattern for the <see cref="Span.ServiceName"/>.</param>
        /// <param name="operationNameGlob">The glob pattern for the <see cref="Span.OperationName"/>.</param>
        /// <param name="resourceNameGlob">The glob pattern for the <see cref="Span.ResourceName"/>.</param>
        /// <param name="tagGlobs">The glob pattern for the <see cref="Span.Tags"/>.</param>
        /// <param name="timeout">The timeout to use for the regexes</param>
        /// <param name="samplingRate">The proportion of spans that are kept. <c>1.0</c> indicates keep all where <c>0.0</c> would be drop all.</param>
        /// <param name="maxPerSecond">The maximum number of spans allowed to be kept per second - <see langword="null"/> indicates that there is no limit</param>
        public SpanSamplingRule(
            string serviceNameGlob,
            string operationNameGlob,
            string resourceNameGlob,
            ICollection<KeyValuePair<string, string>> tagGlobs,
            TimeSpan timeout,
            float samplingRate = 1.0f,
            float? maxPerSecond = null)
        {
            if (string.IsNullOrWhiteSpace(serviceNameGlob))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(serviceNameGlob));
            }

            if (string.IsNullOrWhiteSpace(operationNameGlob))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(operationNameGlob));
            }

            SamplingRate = samplingRate;
            MaxPerSecond = maxPerSecond;

            // null/absent for MaxPerSecond indicates unlimited, which is a negative value in the limiter
            _limiter = MaxPerSecond is null ? new SpanRateLimiter(-1) : new SpanRateLimiter((int?)MaxPerSecond);

            _serviceNameRegex = RegexBuilder.Build(serviceNameGlob, SamplingRulesFormat.Glob, timeout);
            _operationNameRegex = RegexBuilder.Build(operationNameGlob, SamplingRulesFormat.Glob, timeout);
            _resourceNameRegex = RegexBuilder.Build(resourceNameGlob, SamplingRulesFormat.Glob, timeout);
            _tagRegexes = RegexBuilder.Build(tagGlobs, SamplingRulesFormat.Glob, timeout);

            if (_serviceNameRegex is null &&
                _operationNameRegex is null &&
                _resourceNameRegex is null &&
                (_tagRegexes is null || _tagRegexes.Count == 0))
            {
                // if no patterns were specified, this rule always matches (i.e. catch-all)
                _alwaysMatch = true;
            }
        }

        /// <inheritdoc/>
        public float SamplingRate { get; }

        /// <inheritdoc/>
        public float? MaxPerSecond { get; }

        /// <summary>
        ///     Creates <see cref="SpanSamplingRule"/>s from the supplied JSON <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">The JSON-serialized configuration.</param>
        /// <param name="timeout">The timeout to use for regexes</param>
        /// <returns><see cref="IEnumerable{T}"/> of <see cref="SpanSamplingRule"/>.</returns>
        public static IEnumerable<SpanSamplingRule> BuildFromConfigurationString(string configuration, TimeSpan timeout)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(configuration) &&
                    JsonConvert.DeserializeObject<List<SpanSamplingRuleConfig>>(configuration) is { Count: > 0 } rules)
                {
                    var samplingRules = new List<SpanSamplingRule>(rules.Count);

                    foreach (var rule in rules)
                    {
                        samplingRules.Add(
                            new SpanSamplingRule(
                                rule.ServiceNameGlob,
                                rule.OperationNameGlob,
                                rule.ResourceNameGlob,
                                rule.TagGlobs,
                                timeout,
                                rule.SampleRate,
                                rule.MaxPerSecond));
                    }

                    return samplingRules;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Unable to parse the span sampling rules.");
            }

            return [];
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool ShouldSample(Span span)
        {
            if (span is null)
            {
                return false;
            }

            var sampleKeep = SamplingHelpers.SampleByRate(span.SpanId, SamplingRate);

            if (!sampleKeep)
            {
                return false;
            }

            var limitKeep = _limiter.Allowed(span);

            return limitKeep;
        }

        [Serializable]
        internal class SpanSamplingRuleConfig
        {
            [JsonProperty(PropertyName = "service")]
            public string ServiceNameGlob { get; set; } = "*";

            [JsonProperty(PropertyName = "name")]
            public string OperationNameGlob { get; set; } = "*";

            [JsonProperty(PropertyName = "resource")]
            public string ResourceNameGlob { get; set; }

            [JsonProperty(PropertyName = "tags")]
            public Dictionary<string, string> TagGlobs { get; set; }

            [JsonProperty(PropertyName = "sample_rate")]
            public float SampleRate { get; set; } = 1.0f; // default to accept all

            [JsonProperty(PropertyName = "max_per_second")]
            public float? MaxPerSecond { get; set; } = null;
        }
    }
}
