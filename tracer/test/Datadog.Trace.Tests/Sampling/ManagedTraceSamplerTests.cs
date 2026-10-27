// <copyright file="ManagedTraceSamplerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Sampling
{
    public class ManagedTraceSamplerTests
    {
        [Theory]
        [InlineData(null, typeof(AgentSamplingRule))]
        [InlineData("otlp", typeof(DefaultSamplingRule))]
        public void NoSamplingRules_UsesDefaultMechanism(string tracesExporter, Type expectedRuleType)
        {
            var sampler = CreateSampler(tracesExporter, globalSamplingRate: null);

            sampler.GetRules().Should().ContainSingle().Which.Should().BeOfType(expectedRuleType);

            var decision = sampler.MakeSamplingDecision(CreateSpan());

            decision.Mechanism.Should().Be(SamplingMechanism.Default);
            decision.Priority.Should().Be(SamplingPriorityValues.AutoKeep);
            decision.Rate.Should().Be(1);
            decision.LimiterRate.Should().BeNull();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("otlp")]
        public void GlobalSamplingRate_UsesLocalTraceSamplingRule(string tracesExporter)
        {
            var sampler = CreateSampler(tracesExporter, globalSamplingRate: 1.0);

            var decision = sampler.MakeSamplingDecision(CreateSpan());

            decision.Mechanism.Should().Be(SamplingMechanism.LocalTraceSamplingRule);
            decision.Priority.Should().Be(SamplingPriorityValues.UserKeep);
            decision.Rate.Should().Be(1);
            decision.LimiterRate.Should().NotBeNull();
        }

        private static ManagedTraceSampler CreateSampler(string tracesExporter, double? globalSamplingRate)
        {
            var config = new Dictionary<string, object>();

            if (tracesExporter is not null)
            {
                config[ConfigurationKeys.OpenTelemetry.TracesExporter] = tracesExporter;
            }

            if (globalSamplingRate is not null)
            {
                config[ConfigurationKeys.GlobalSamplingRate] = globalSamplingRate.Value;
            }

            return new ManagedTraceSampler(TracerSettings.Create(config));
        }

        private static Span CreateSpan() => new(new SpanContext(traceId: 1, spanId: 1), DateTimeOffset.UtcNow);
    }
}
