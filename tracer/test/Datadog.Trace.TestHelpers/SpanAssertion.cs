// <copyright file="SpanAssertion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers
{
    public class SpanAssertion
    {
        private const string MatchesFailureStringFormat = "{0} \"{1}\" was expected to have value {2}, but the value is \"{3}\"";
        private const string PresentFailureStringFormat = "{0} \"{1}\" was expected to be present";
        private const string MatchesOneOfFailure = "{0} \"{1}\" was expected to have one of the following values {2}, but the value is \"{3}\"";

        protected static string GenerateMatchesFailureString(string propertyKind, string propertyName, string expectedValue, string actualValue) =>
            string.Format(MatchesFailureStringFormat, propertyKind, propertyName, expectedValue, actualValue);

        protected static string GenerateMatchesOneOfFailureString(string propertyKind, string propertyName, string expectedValue, string actualValue) =>
            string.Format(MatchesOneOfFailure, propertyKind, propertyName, expectedValue, actualValue);

        protected static string GeneratePresentFailureString(string propertyKind, string propertyName) =>
            string.Format(PresentFailureStringFormat, propertyKind, propertyName);
    }
}
