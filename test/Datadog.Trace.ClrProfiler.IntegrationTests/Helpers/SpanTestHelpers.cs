using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class SpanTestHelpers
    {
        public static void AssertExpectationsMet<T>(
            List<T> expectations,
            List<MockTracerAgent.Span> spans)
            where T : SpanExpectation
        {
            Assert.True(spans.Count >= expectations.Count, $"Expected at least {expectations.Count} spans, received {spans.Count}");

            var failures = new List<string>();

            foreach (var expectation in expectations)
            {
                var possibleSpans =
                    spans
                       .Where(s => !s.Inspected)
                       .Where(s => expectation.ShouldInspect(s))
                       .ToList();

                var count = possibleSpans.Count;

                var detail = $"({expectation.Detail()})";

                if (count == 0)
                {
                    failures.Add($"No spans for: {detail}");
                    continue;
                }

                var resultSpan = possibleSpans.First();

                resultSpan.Inspected = true;

                if (!expectation.MeetsExpectations(resultSpan, out var failureMessage))
                {
                    failures.Add($"{detail} failed with: {failureMessage}");
                }
            }

            var finalMessage = Environment.NewLine + string.Join(Environment.NewLine, failures.Select(f => " - " + f));

            Assert.True(!failures.Any(), finalMessage);

            var uninspectedSpans = spans.Count(s => s.Inspected);
            Assert.True(uninspectedSpans == 0, $"There were {uninspectedSpans} spans unaccounted for.");
        }
    }
}
