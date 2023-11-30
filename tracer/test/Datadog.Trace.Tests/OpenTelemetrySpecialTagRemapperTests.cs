// <copyright file="OpenTelemetrySpecialTagRemapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetrySpecialTagRemapperTests))]
    public class OpenTelemetrySpecialTagRemapperTests
    {
        private readonly Tracer _tracer;

        public OpenTelemetrySpecialTagRemapperTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        }

        [Fact]
        public void OperationName_Tag_Should_Override_OperationName()
        {
            var expected = "overridden.name";
            var inputValue = expected.ToUpperInvariant(); // we are required to lowercase OperationName
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "operation.name", inputValue }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow);
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.OperationName);
        }

        [Fact]
        public void ResourceName_Tag_Should_Override_ResourceName()
        {
            var expected = "overridden.name";
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "resource.name", expected }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow);
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.ResourceName);
        }

        [Fact]
        public void ServiceName_Tag_Should_Override_ServiceName()
        {
            var expected = "overridden.name";
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "service.name", expected }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow);
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.ServiceName);
        }

        [Fact]
        public void SpanType_Tag_Should_Override_SpanType()
        {
            var expected = "overridden.name";
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "span.type", expected }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow);
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.Type);
        }

        [Theory]
        [InlineData("true", 1.0)]
        [InlineData("false", 0.0)]
        public void AnalyticsEvent_Tag_Should_Override_AnalyticsSamplingRateMetric(string value, double expected)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "analytics.event", value }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow);
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.GetMetric(Tags.Analytics));
        }
    }
}
