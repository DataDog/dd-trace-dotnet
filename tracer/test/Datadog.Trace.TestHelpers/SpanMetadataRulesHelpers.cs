// <copyright file="SpanMetadataRulesHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    public static partial class SpanMetadataRules
    {
        internal static (string PropertyName, string Result) Name(MockSpan span) => (nameof(span.Name), span.Name);

        internal static (string PropertyName, string Result) Type(MockSpan span) => (nameof(span.Type), span.Type);
    }

#pragma warning disable SA1402 // File may only contain a single type

#pragma warning disable SA1201 // Elements should appear in the correct order
    public class Result
    {
        public MockSpan Span { get; }

        public List<string> Errors { get; }

        public bool Success
        {
            get => Errors.Count == 0;
        }

        public Result(MockSpan span)
        {
            Span = span;
            Errors = new List<string>();
        }

        public static Result FromSpan(MockSpan span)
        {
            return new Result(span);
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
            var t = new SpanTagAssertion(this);
            tagAssertions(t);
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
