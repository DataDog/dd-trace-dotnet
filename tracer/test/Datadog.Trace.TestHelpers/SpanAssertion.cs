// <copyright file="SpanAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.TestHelpers
{
    public class SpanAssertion
    {
        private const string MatchesFailureStringFormat = "{0} \"{1}\" was expected to have value \"{2}\", but the value is \"{3}\"";
        private const string PresentFailureStringFormat = "{0} {1} was expected to be present";
        private const string PresentFoundMultipleFailureStringFormat = "{0} {1} was expected to be present, but found more than one of the exclusive values: {2}";
        private const string MatchesOneOfFailure = "{0} \"{1}\" was expected to have one of the following values \"{2}\", but the value is \"{3}\"";
        private const string NoRemainingTagsFailureFormat = "Expected to have no remaining tags, but the following tags were found: [{0}]";

        protected static string GenerateMatchesFailureString(string propertyKind, string propertyName, string expectedValue, string actualValue) =>
            string.Format(MatchesFailureStringFormat, propertyKind, propertyName, expectedValue, actualValue ?? "(null)");

        protected static string GenerateMatchesOneOfFailureString(string propertyKind, string propertyName, string expectedValue, string actualValue) =>
            string.Format(MatchesOneOfFailure, propertyKind, propertyName, expectedValue, actualValue ?? "(null)");

        protected static string GeneratePresentFailureString(string propertyKind, string propertyName) =>
            string.Format(PresentFailureStringFormat, propertyKind, propertyName);

        protected static string GeneratePresentFoundMultipleFailureString(string propertyKind, string propertyName, string found) =>
            string.Format(PresentFoundMultipleFailureStringFormat, propertyKind, propertyName, found);

        protected static string GenerateNoRemainingTagsFailureString(IDictionary<string, string> tags)
        {
            var stringArray = tags.ToArray().Select(kvp => $"\"{kvp.Key}\"=\"{kvp.Value}\"");
            return string.Format(NoRemainingTagsFailureFormat, string.Join(",", stringArray));
        }
    }
}
