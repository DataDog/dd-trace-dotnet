// <copyright file="Result.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class Result
    {
        public static readonly Result DefaultSuccess = FromSpan(null);

        private bool _propertiesInvoked;
        private bool _additionalTagsInvoked;
        private bool _tagsInvoked;
        private bool _metricsInvoked;
        private Action<SpanAdditionalTagsAssertion> _additionalTagAssertions;

        private Result(MockSpan span, ISet<string> excludeTags)
        {
            Span = span;
            ExcludeTags = excludeTags;
            Errors = new List<string>();
        }

        public MockSpan Span { get; }

        public ISet<string> ExcludeTags { get; }

        public List<string> Errors { get; }

        public bool Success
        {
            get => Errors.Count == 0;
        }

        public static Result FromSpan(MockSpan span, ISet<string> excludeTags = null)
        {
            return new Result(span, excludeTags ?? new HashSet<string>());
        }

        public Result WithFailure(string failureMessage)
        {
            Errors.Add(failureMessage);
            return this;
        }

        public Result WithIntegrationName(string name) => this;

        public Result WithMarkdownSection(string name) => this;

        public Result Properties(Action<SpanPropertyAssertion> propertyAssertions)
        {
            if (_propertiesInvoked)
            {
                throw new InvalidOperationException("Result.Properties() may only be invoked once per integration");
            }

            _propertiesInvoked = true;
            var p = new SpanPropertyAssertion(this);
            propertyAssertions(p);
            return this;
        }

        public Result Tags(Action<SpanTagAssertion<string>> tagAssertions)
        {
            if (_tagsInvoked)
            {
                throw new InvalidOperationException("Result.Tags() may only be invoked once per integration");
            }

            _tagsInvoked = true;
            var tags = new Dictionary<string, string>(this.Span.Tags);
            var t = new SpanTagAssertion<string>(this, tags);
            tagAssertions(t);

            if (_additionalTagAssertions is not null)
            {
                var at = new SpanAdditionalTagsAssertion(this, tags);
                _additionalTagAssertions(at);
            }

            SpanTagAssertion<string>.DefaultTagAssertions(t);
            SpanTagAssertion<string>.AssertNoRemainingTags(t);
            return this;
        }

        public Result Metrics(Action<SpanTagAssertion<double>> metricAssertions)
        {
            if (_metricsInvoked)
            {
                throw new InvalidOperationException("Result.Metrics() may only be invoked once per integration");
            }

            _metricsInvoked = true;
            var metrics = new Dictionary<string, double>(this.Span.Metrics);
            var t = new SpanTagAssertion<double>(this, metrics);
            metricAssertions(t);

            SpanTagAssertion<double>.DefaultMetricAssertions(t);
            SpanTagAssertion<double>.AssertNoRemainingTags(t);
            return this;
        }

        public Result AdditionalTags(Action<SpanAdditionalTagsAssertion> tagAssertions)
        {
            if (_additionalTagsInvoked)
            {
                throw new InvalidOperationException("Result.AdditionalTags() may only be invoked once per integration");
            }
            else if (_tagsInvoked)
            {
                throw new InvalidOperationException("Result.AdditionalTags() must be invoked before Result.Tags()");
            }

            _additionalTagsInvoked = true;
            _additionalTagAssertions = tagAssertions;
            return this;
        }

        public override string ToString()
        {
            string errorMessage = string.Concat(Errors.Select(s => $"{Environment.NewLine}- {s}"));

            return $"Result: {Success}{Environment.NewLine}"
                 + $"Span: {Span}{Environment.NewLine}"
                 + $"Errors:{errorMessage}";
        }
    }
}
