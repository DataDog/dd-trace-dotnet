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

        public Result Properties(Action<SpanPropertyAssertion> propertyAssertions)
        {
            var p = new SpanPropertyAssertion(this);
            propertyAssertions(p);
            return this;
        }

        public Result Tags(Action<SpanTagAssertion> tagAssertions)
        {
            var t = new SpanTagAssertion(this, this.Span.Tags);
            tagAssertions(t);

            SpanTagAssertion.DefaultTagAssertions(t);
            SpanTagAssertion.AssertNoRemainingTags(t);
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
