using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebServerSpanExpectation
    {
        public string OriginalUri { get; set; }

        public string OperationName { get; set; }

        public string ServiceName { get; set; }

        public string Type { get; set; }

        public string ResourceName { get; set; }

        public string StatusCode { get; set; }

        public string HttpMethod { get; set; }

        public Func<MockTracerAgent.Span, List<string>> CustomAssertion { get; set; }

        public virtual bool IsSimpleMatch(MockTracerAgent.Span span)
        {
            return span.Resource == ResourceName
                && span.Name == OperationName
                && span.Type == Type;
        }

        public virtual bool IsMatch(MockTracerAgent.Span span, out string message)
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

            var expectedResourceName = ResourceName.TrimEnd();

            if (span.Resource != expectedResourceName)
            {
                mismatches.Add(FailureMessage(nameof(ResourceName), actual: span.Resource, expected: expectedResourceName));
            }

            var actualStatusCode = GetTag(span, Tags.HttpStatusCode);
            var actualHttpMethod = GetTag(span, Tags.HttpMethod);

            if (StatusCode != null && actualStatusCode != StatusCode)
            {
                mismatches.Add(FailureMessage(nameof(StatusCode), actual: actualStatusCode, expected: StatusCode));
            }

            if (actualHttpMethod != HttpMethod)
            {
                mismatches.Add(FailureMessage(nameof(HttpMethod), actual: actualHttpMethod, expected: HttpMethod));
            }

            if (CustomAssertion != null)
            {
                mismatches.AddRange(CustomAssertion(span));
            }

            message = string.Join(", ", mismatches);

            return !mismatches.Any();
        }

        protected string FailureMessage(string name, string actual, string expected)
        {
            return $"({name} mismatch: actual: {actual ?? "NULL"}, expected: {expected ?? "NULL"})";
        }

        protected string GetTag(MockTracerAgent.Span span, string tag)
        {
            if (span.Tags.ContainsKey(tag))
            {
                return span.Tags[tag];
            }

            return null;
        }
    }
}
