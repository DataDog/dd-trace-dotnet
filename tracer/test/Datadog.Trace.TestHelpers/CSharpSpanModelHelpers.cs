// <copyright file="CSharpSpanModelHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
#pragma warning disable SA1601 // Partial elements should be documented
    public static partial class CSharpTracingIntegrationRules
    {
        private const string MatchesFailureStringFormat = "{0} \"{1}\" was expected to have value {2}, but the value is \"{3}\"";
        private const string PresentFailureStringFormat = "{0} \"{1}\" was expected to be present";
        private const string MatchesOneOfFailure = "{0} \"{1}\" was expected to have one of the following values {2}, but the value is \"{3}\"";

        internal static (string PropertyName, string Result) Name(MockSpan span) => (nameof(span.Name), span.Name);

        internal static (string PropertyName, string Result) Type(MockSpan span) => (nameof(span.Type), span.Type);

        internal static Func<string, string> GenerateMatchesFailureString(string propertyKind, string propertyName, string expectedValue) =>
            (actualValue) => string.Format(MatchesFailureStringFormat, propertyKind, propertyName, expectedValue, actualValue);

        internal static Func<string, string> GenerateMatchesOneOfFailureString(string propertyKind, string propertyName, string expectedValue) =>
            (actualValue) => string.Format(MatchesOneOfFailure, propertyKind, propertyName, expectedValue, actualValue);

        internal static string GeneratePresentFailureString(string propertyKind, string propertyName) =>
            string.Format(PresentFailureStringFormat, propertyKind, propertyName);

        internal static Result IsOptional(this Result span, string result) => span;

        internal static Result IsPresent(this Result span, string result, string failureString) =>
            result switch
            {
                null => span.WithFailure(failureString),
                _ => span,
            };

        internal static Result Matches(this Result span, string expectedValue, string result, Func<string, string> failureStringFunc) =>
            result switch
            {
                string actualValue when actualValue != expectedValue => span.WithFailure(failureStringFunc(result)),
                _ => span,
            };

        internal static Result MatchesOneOf(this Result span, string[] expectedValueArray, string result, Func<string, string> failureStringFunc) =>
            expectedValueArray.Where(s => s == result).SingleOrDefault() switch
            {
                null => span.WithFailure(failureStringFunc(result)),
                _ => span,
            };

        internal static Result PropertyMatches(this Result span, Func<MockSpan, (string PropertyName, string Result)> property, string expectedValue)
        {
            (string propertyName, string result) = property(span.Span);
            return span.Matches(expectedValue, result, GenerateMatchesFailureString("property", propertyName, expectedValue));
        }

        internal static Result PropertyMatchesOneOf(this Result span, Func<MockSpan, (string PropertyName, string Result)> property, params string[] expectedValueArray)
        {
            (string propertyName, string result) = property(span.Span);
            string expectedValueString = "["
                                         + string.Join(",", expectedValueArray.Select(s => $"\"{s}\"").ToArray())
                                         + "]";
            return span.MatchesOneOf(expectedValueArray, result, GenerateMatchesOneOfFailureString("property", propertyName, expectedValueString));
        }

        internal static Result TagIsOptional(this Result span, string tagName) =>
            span.IsOptional(span.Span.GetTag(tagName));

        internal static Result TagIsPresent(this Result span, string tagName) =>
            span.IsPresent(span.Span.GetTag(tagName), GeneratePresentFailureString("tag", tagName));

        internal static Result TagMatches(this Result span, string tagName, string expectedValue) =>
            span.Matches(expectedValue, span.Span.GetTag(tagName), GenerateMatchesFailureString("tag", tagName, expectedValue));

        internal static Result TagMatchesOneOf(this Result span, string tagName, params string[] expectedValueArray)
        {
            string expectedValueString = "["
                                         + string.Join(",", expectedValueArray.Select(s => $"\"{s}\"").ToArray())
                                         + "]";
            return span.MatchesOneOf(expectedValueArray, span.Span.GetTag(tagName), GenerateMatchesOneOfFailureString("tag", tagName, expectedValueString));
        }
    }

#pragma warning disable SA1201 // Elements should appear in the correct order
    public struct Result
    {
        public MockSpan Span;
        public string Message;
        public bool Success;

        public Result(MockSpan span, string message)
        {
            Span = span;
            Message = message;
            Success = true;
        }

        public static Result FromSpan(MockSpan span)
        {
            return new Result(span, string.Empty);
        }

        public Result WithFailure(string failureMessage)
        {
            Success = false;

            if (Message.Length == 0)
            {
                Message = failureMessage;
            }
            else
            {
                Message = Message + ";" + failureMessage;
            }

            return this;
        }
    }
}
