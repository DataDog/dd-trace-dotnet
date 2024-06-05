// <copyright file="CustomSamplingRule.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Datadog.Trace.Sampling
{
    internal abstract class CustomSamplingRule : ISamplingRule
    {
        private readonly float _samplingRate;
        private readonly bool _alwaysMatch;

        // TODO consider moving toward these https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/System/Text/SimpleRegex.cs
        private readonly Regex? _serviceNameRegex;
        private readonly Regex? _operationNameRegex;
        private readonly Regex? _resourceNameRegex;
        private readonly List<KeyValuePair<string, Regex?>>? _tagRegexes;

        private bool _regexTimedOut;

        protected CustomSamplingRule(
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

        public abstract string Provenance { get; }

        public abstract int SamplingMechanism { get; }

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
    }
}
