using System.Collections.Generic;
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

        public bool IsMatch(MockTracerAgent.Span span, out string message)
        {
            var match = true;
            var messages = new List<string>();

            if (span.Name != OperationName)
            {
                match = false;
                messages.Add($"({nameof(OperationName)} mismatch: actual: {span.Name ?? "NULL"}, expected: {OperationName})");
            }

            if (span.Service != ServiceName)
            {
                match = false;
                messages.Add($"({nameof(ServiceName)} mismatch: actual: {span.Service ?? "NULL"}, expected: {ServiceName})");
            }

            if (span.Type != Type)
            {
                match = false;
                messages.Add($"({nameof(Type)} mismatch: actual: {span.Type ?? "NULL"}, expected: {Type})");
            }

            if (span.Service != ServiceName)
            {
                match = false;
                messages.Add($"({nameof(ServiceName)} mismatch: actual: {span.Service ?? "NULL"}, expected: {OperationName})");
            }

            var expectedResourceName = ResourceName.TrimEnd();

            if (span.Resource != expectedResourceName)
            {
                match = false;
                messages.Add($"({nameof(ResourceName)} mismatch: actual: {span.Resource ?? "NULL"}, expected: {expectedResourceName})");
            }

            var actualStatusCode = GetTag(span, Tags.HttpStatusCode);
            var actualHttpMethod = GetTag(span, Tags.HttpMethod);

            if (actualStatusCode != StatusCode)
            {
                match = false;
                messages.Add($"{nameof(StatusCode)} mismatch: actual: {actualStatusCode ?? "NULL"}, expected: {StatusCode}");
            }

            if (actualHttpMethod != HttpMethod)
            {
                match = false;
                messages.Add($"{nameof(HttpMethod)} mismatch: actual: {actualHttpMethod ?? "NULL"}, expected: {HttpMethod}");
            }

            message = string.Join(", ", messages);

            return match;
        }

        private string GetTag(MockTracerAgent.Span span, string tag)
        {
            if (span.Tags.ContainsKey(tag))
            {
                return span.Tags[tag];
            }

            return null;
        }
    }
}
