using System.Collections.Generic;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class GraphQLSpanExpectation : WebServerSpanExpectation
    {
        public GraphQLSpanExpectation(string serviceName, string operationName)
            : base(serviceName, operationName, SpanTypes.GraphQL)
        {
            RegisterDelegateExpectation(ExpectErrorMatch);
            RegisterTagExpectation(nameof(Tags.GraphQLSource), expected: GraphQLSource);
            RegisterTagExpectation(nameof(Tags.GraphQLOperationType), expected: GraphQLOperationType);
        }

        public string GraphQLRequestBody { get; set; }

        public string GraphQLOperationType { get; set; }

        public string GraphQLOperationName { get; set; }

        public string GraphQLSource { get; set; }

        public bool IsGraphQLError { get; set; }

        private IEnumerable<string> ExpectErrorMatch(MockTracerAgent.Span span)
        {
            var error = GetTag(span, Tags.ErrorMsg);
            if (string.IsNullOrEmpty(error))
            {
                if (IsGraphQLError)
                {
                    yield return $"Expected an error message but {Tags.ErrorMsg} tag is missing or empty.";
                }
            }
            else
            {
                if (!IsGraphQLError)
                {
                    yield return $"Expected no error message but {Tags.ErrorMsg} tag was {error}.";
                }
            }
        }
    }
}
