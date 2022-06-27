// <copyright file="SpanTagAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class SpanTagAssertion : SpanAssertion
    {
        private readonly Result _result;

        internal SpanTagAssertion(Result result)
        {
            _result = result;
        }

        public SpanTagAssertion IsPresent(string tagName)
        {
            if (_result.Span.GetTag(tagName) is null)
            {
                _result.WithFailure(GeneratePresentFailureString("tag", tagName));
            }

            return this;
        }

        public SpanTagAssertion IsOptional(string key) => this;

        public SpanTagAssertion Matches(string tagName, string expectedValue)
        {
            var actualValue = _result.Span.GetTag(tagName);
            if (actualValue != expectedValue)
            {
                _result.WithFailure(GenerateMatchesFailureString("tag", tagName, expectedValue, actualValue));
            }

            return this;
        }

        public SpanTagAssertion MatchesOneOf(string tagName, params string[] expectedValues)
        {
            var actualValue = _result.Span.GetTag(tagName);
            if (expectedValues.Where(s => s == actualValue).SingleOrDefault() is null)
            {
                string expectedValueString = "["
                             + string.Join(",", expectedValues.Select(s => $"\"{s}\"").ToArray())
                             + "]";

                _result.WithFailure(GenerateMatchesOneOfFailureString("tag", tagName, expectedValueString, actualValue));
            }

            return this;
        }
    }
}
