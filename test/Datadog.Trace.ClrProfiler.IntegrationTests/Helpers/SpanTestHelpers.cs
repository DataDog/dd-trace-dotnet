// <copyright file="SpanTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
            var remainingSpans = spans.Select(s => s).ToList();

            foreach (var expectation in expectations)
            {
                var possibleSpans =
                    remainingSpans
                       .Where(s => expectation.Matches(s))
                       .ToList();

                if (possibleSpans.Count == 0)
                {
                    failures.Add($"No spans for: {expectation}");
                    continue;
                }

                var resultSpan = possibleSpans.First();

                if (!remainingSpans.Remove(resultSpan))
                {
                    throw new Exception("Failed to remove an inspected span, can't trust this test.'");
                }

                if (!expectation.MeetsExpectations(resultSpan, out var failureMessage))
                {
                    failures.Add($"{expectation} failed with: {failureMessage}");
                }
            }

            var finalMessage = Environment.NewLine + string.Join(Environment.NewLine, failures.Select(f => " - " + f));

            Assert.True(!failures.Any(), finalMessage);
            Assert.True(remainingSpans.Count == 0, $"There were {remainingSpans.Count} spans unaccounted for.");
        }
    }
}
