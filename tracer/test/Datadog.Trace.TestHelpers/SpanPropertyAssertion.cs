// <copyright file="SpanPropertyAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class SpanPropertyAssertion : SpanAssertion
    {
        private readonly Result _result;

        internal SpanPropertyAssertion(Result result)
        {
            _result = result;
        }

        public SpanPropertyAssertion Matches(Func<MockSpan, (string PropertyName, string Result)> property, string expectedValue)
        {
            (string propertyName, string actualValue) = property(_result.Span);
            if (actualValue != expectedValue)
            {
                _result.WithFailure(GenerateMatchesFailureString("property", propertyName, expectedValue, actualValue));
            }

            return this;
        }

        public SpanPropertyAssertion MatchesOneOf(Func<MockSpan, (string PropertyName, string Result)> property, params string[] expectedValues)
        {
            (string propertyName, string actualValue) = property(_result.Span);
            if (expectedValues.Where(s => s == actualValue).SingleOrDefault() is null)
            {
                string expectedValueString = "["
                             + string.Join(",", expectedValues.Select(s => $"\"{s}\"").ToArray())
                             + "]";

                _result.WithFailure(GenerateMatchesOneOfFailureString("property", propertyName, expectedValueString, actualValue));
            }

            return this;
        }
    }
}
