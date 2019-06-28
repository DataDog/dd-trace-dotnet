using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class WebServerTestHelpers
    {
        public static void AssertExpectationsMet(
            List<WebServerSpanExpectation> expectations,
            List<MockTracerAgent.Span> spans)
        {

            Assert.True(spans.Count >= expectations.Count, $"Expected at least {expectations.Count} spans, received {spans.Count}");

            var failures = new List<string>();

            foreach (var expectation in expectations)
            {
                var possibleSpans =
                    spans
                       .Where(s => s.Resource == expectation.ResourceName)
                       .Where(s => s.Name == expectation.OperationName)
                       .Where(s => s.Type == expectation.Type)
                       .ToList();

                var count = possibleSpans.Count();

                if (count == 0)
                {
                    failures.Add($"No spans for resource: {expectation.ResourceName}");
                    continue;
                }

                if (count > 1)
                {
                    failures.Add($"Too many spans for resource: {expectation.ResourceName}");
                    continue;
                }

                var resultSpan = possibleSpans.Single();

                if (!expectation.IsMatch(resultSpan, out string failureMessage))
                {
                    failures.Add($"{expectation.ResourceName} failed with: {failureMessage}");
                }
            }

            var finalMessage = Environment.NewLine + string.Join(Environment.NewLine, failures.Select(f => " - " + f));

            Assert.True(!failures.Any(), finalMessage);
        }
    }
}
