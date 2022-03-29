// <copyright file="EquivalencyAssertionOptionsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using FluentAssertions.Equivalency;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Helpers
{
    public static class EquivalencyAssertionOptionsExtensions
    {
        public static EquivalencyAssertionOptions<MockSpan> ExcludingDefaultSpanProperties(this EquivalencyAssertionOptions<MockSpan> options)
        {
            return options.Excluding(s => s.TraceId)
                          .Excluding(s => s.SpanId)
                          .Excluding(s => s.Start)
                          .Excluding(s => s.Duration)
                          .Excluding(s => s.ParentId);
        }

        public static EquivalencyAssertionOptions<MockSpan> AssertTagsMatchAndSpecifiedTagsPresent(this EquivalencyAssertionOptions<MockSpan> options, params string[] presentTags)
        {
            return options.Using<Dictionary<string, string>>(ctx =>
            {
                ctx.Subject.Should().ContainKeys(presentTags);
                ctx.Subject.ExceptKeys(presentTags).Should().Equal(ctx.Expectation);
            }).When(info => info.Path.EndsWith("Tags"));
        }

        public static EquivalencyAssertionOptions<MockSpan> AssertMetricsMatchExcludingKeys(this EquivalencyAssertionOptions<MockSpan> options, params string[] excludedKeys)
        {
            return options.Using<Dictionary<string, double>>(ctx =>
            {
                ctx.Subject.ExceptKeys(excludedKeys).Should().Equal(ctx.Expectation);
            }).When(info => info.Path.EndsWith("Metrics"));
        }
    }
}
