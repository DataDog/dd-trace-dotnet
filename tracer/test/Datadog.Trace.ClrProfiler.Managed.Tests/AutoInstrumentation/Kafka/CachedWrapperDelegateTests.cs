// <copyright file="CachedWrapperDelegateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Confluent.Kafka;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Kafka
{
    public class CachedWrapperDelegateTests
    {
        [Fact]
        public void CanCreateWrapperDelegate()
        {
            var wasOriginalInvoked = false;
            var testReport = new DeliveryReport<string, string>();
            Action<DeliveryReport<string, string>> original = x =>
            {
                wasOriginalInvoked = true;
                x.Should().BeSameAs(testReport);
            };

            var tracer = GetTracer();
            var span = tracer.StartSpan("Test operation");
            var wrapper = KafkaProduceSyncDeliveryHandlerIntegration.CachedWrapperDelegate<Action<DeliveryReport<string, string>>>.CreateWrapper(original, span);

            wrapper.Invoke(testReport);
            wasOriginalInvoked.Should().BeTrue();
            span.IsFinished.Should().BeTrue();
        }

        [Fact]
        public void CanCreateMultipleWrapperDelegates()
        {
            var stringReport = new DeliveryReport<string, string>();
            var intReport = new DeliveryReport<int, string>();

            var tracer = GetTracer();

            var stringSpan = tracer.StartSpan("Test string message operation");
            var stringWrapper = KafkaProduceSyncDeliveryHandlerIntegration
                         .CachedWrapperDelegate<Action<DeliveryReport<string, string>>>.CreateWrapper(x => { }, stringSpan);
            stringWrapper.Invoke(stringReport);

            var intSpan = tracer.StartSpan("Test int message operation");
            var intWrapper = KafkaProduceSyncDeliveryHandlerIntegration
                               .CachedWrapperDelegate<Action<DeliveryReport<int, string>>>.CreateWrapper(x => { }, intSpan);
            intWrapper.Invoke(intReport);

            stringSpan.IsFinished.Should().BeTrue();
            intSpan.IsFinished.Should().BeTrue();
        }

        private static Tracer GetTracer()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }
    }
}
