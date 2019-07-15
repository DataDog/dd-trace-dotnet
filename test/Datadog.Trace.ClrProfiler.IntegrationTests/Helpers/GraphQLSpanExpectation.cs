using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class GraphQLSpanExpectation : WebServerSpanExpectation
    {
        public string GraphQLRequestBody { get; set; }

        public string GraphQLOperationType { get; set; }

        public string GraphQLOperationName { get; set; }

        public string GraphQLSource { get; set; }

        public bool IsGraphQLError { get; set; }

        public override bool IsSimpleMatch(MockTracerAgent.Span span)
        {
            return span.Name == OperationName
                && span.Type == Type
                && !string.IsNullOrEmpty(GetTag(span, Tags.ErrorMsg)) == IsGraphQLError
                && SourceStringsAreEqual(GetTag(span, Tags.GraphQLSource), GraphQLSource);
        }

        public override bool IsMatch(MockTracerAgent.Span span, out string message)
        {
            var mismatches = new List<string>();

            if (span.Name != OperationName)
            {
                mismatches.Add(FailureMessage(nameof(OperationName), actual: span.Name, expected: OperationName));
            }

            if (span.Service != ServiceName)
            {
                mismatches.Add(FailureMessage(nameof(ServiceName), actual: span.Service, expected: ServiceName));
            }

            if (span.Type != Type)
            {
                mismatches.Add(FailureMessage(nameof(Type), actual: span.Type, expected: Type));
            }

            var spanIsError = !string.IsNullOrEmpty(GetTag(span, Tags.ErrorMsg));

            if (spanIsError != IsGraphQLError)
            {
                mismatches.Add(FailureMessage(nameof(IsGraphQLError), actual: spanIsError.ToString(), expected: IsGraphQLError.ToString()));
            }

            var actualSource = GetTag(span, Tags.GraphQLSource);

            if (!SourceStringsAreEqual(actualSource, GraphQLSource))
            {
                mismatches.Add(FailureMessage(nameof(GraphQLSource), actual: actualSource, expected: GraphQLSource));
            }

            if (CustomAssertion != null)
            {
                mismatches.AddRange(CustomAssertion(span));
            }

            message = string.Join(", ", mismatches);

            return !mismatches.Any();
        }

        private static bool SourceStringsAreEqual(string source1, string source2)
        {
            return string.Equals(
                source1.Replace(" ", string.Empty),
                source2.Replace(" ", string.Empty),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
