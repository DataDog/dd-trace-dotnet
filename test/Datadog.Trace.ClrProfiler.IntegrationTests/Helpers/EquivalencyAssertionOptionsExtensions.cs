using Datadog.Trace.TestHelpers;
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
    }
}
