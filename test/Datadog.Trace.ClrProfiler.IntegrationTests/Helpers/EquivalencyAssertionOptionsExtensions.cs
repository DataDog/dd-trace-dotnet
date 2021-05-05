using System;
using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Equivalency;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public static class EquivalencyAssertionOptionsExtensions
    {
        public static EquivalencyAssertionOptions<MockTracerAgent.Span> ExcludingDefaultSpanProperties(this EquivalencyAssertionOptions<MockTracerAgent.Span> options)
        {
            return options.Excluding(s => s.TraceId)
                          .Excluding(s => s.SpanId)
                          .Excluding(s => s.Start)
                          .Excluding(s => s.Duration)
                          .Excluding(s => s.ParentId);
        }

        public static EquivalencyAssertionOptions<MockTracerAgent.Span> AssertTagsMatchAndSpecifiedTagsPresent(this EquivalencyAssertionOptions<MockTracerAgent.Span> options, params string[] presentTags)
        {
            return options.Using<Dictionary<string, string>>(ctx =>
            {
                ctx.Subject.Should().ContainKeys(presentTags);
                ctx.Subject.ExceptKeys(presentTags).Should().Equal(ctx.Expectation);
            }).When(info => info.SelectedMemberPath.EndsWith("Tags"));
        }

        public static EquivalencyAssertionOptions<MockTracerAgent.Span> AssertMetricsMatchExcludingKeys(this EquivalencyAssertionOptions<MockTracerAgent.Span> options, params string[] excludedKeys)
        {
            return options.Using<Dictionary<string, double>>(ctx =>
            {
                ctx.Subject.ExceptKeys(excludedKeys).Should().Equal(ctx.Expectation);
            }).When(info => info.SelectedMemberPath.EndsWith("Metrics"));
        }
    }
}
