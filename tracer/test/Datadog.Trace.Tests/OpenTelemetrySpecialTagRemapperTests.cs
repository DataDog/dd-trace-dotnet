// <copyright file="OpenTelemetrySpecialTagRemapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetrySpecialTagRemapperTests))]
    public class OpenTelemetrySpecialTagRemapperTests : IAsyncLifetime
    {
        private readonly ScopedTracer _tracer;

        public OpenTelemetrySpecialTagRemapperTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            _tracer = TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync() => await _tracer.DisposeAsync();

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
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
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
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
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
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
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
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
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
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.GetMetric(Tags.Analytics));
        }

#if NET6_0_OR_GREATER
        [Theory]
        [InlineData((int)Datadog.Trace.Activity.DuckTypes.ActivityStatusCode.Ok, "STATUS_CODE_OK")]
        [InlineData((int)Datadog.Trace.Activity.DuckTypes.ActivityStatusCode.Error, "STATUS_CODE_ERROR")]
        [InlineData((int)Datadog.Trace.Activity.DuckTypes.ActivityStatusCode.Unset, "STATUS_CODE_UNSET")]
        public void ActivityStatus_IsReflectedInOtelStatusCode_WhenOtelSemanticsEnabled(int activityStatus, string expectedOtelStatusCode)
        {
            var activityMock = new Mock<IActivity6>();
            activityMock.Setup(x => x.TagObjects).Returns(new Dictionary<string, object>());
            activityMock.Setup(x => x.Status).Returns((Datadog.Trace.Activity.DuckTypes.ActivityStatusCode)activityStatus);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span, openTelemetrySemanticsEnabled: true);

            span.GetTag("otel.status_code").Should().Be(expectedOtelStatusCode);
        }
#endif

        [Theory]
        [InlineData("OK", "STATUS_CODE_OK")]
        [InlineData("ERROR", "STATUS_CODE_ERROR")]
        [InlineData("UNSET", "STATUS_CODE_UNSET")]
        [InlineData(null, "STATUS_CODE_UNSET")]
        public void ShortFormOtelStatusCode_IsNormalized_WhenActivity6Unavailable(string shortFormValue, string expectedOtelStatusCode)
        {
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Internal);
            var tagObjects = new Dictionary<string, object>();
            if (shortFormValue is not null)
            {
                tagObjects["otel.status_code"] = shortFormValue;
            }

            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span, openTelemetrySemanticsEnabled: true);

            span.GetTag("otel.status_code").Should().Be(expectedOtelStatusCode);
        }

        [Fact]
        public void HttpStatusCode_WithIntegerValue_IsNotRemapped_WhenOtelSemanticsEnabled()
        {
            const string key = "http.status_code";
            const int statusCode = 200;
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object> { { key, statusCode } };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span, openTelemetrySemanticsEnabled: true);

            span.GetTag(Tags.HttpStatusCode).Should().BeNull();
            span.GetMetric(key).Should().Be((double)statusCode);
        }

        [Fact]
        public void HttpResponseStatusCode_WithIntegerValue_IsNotRemapped_WhenOtelSemanticsEnabled()
        {
            const string key = "http.response.status_code";
            const int statusCode = 200;
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object> { { key, statusCode } };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var spanContext = _tracer.CreateSpanContext(parent: null, serviceName: null, traceId: new TraceId(0, 1), spanId: 1);
            var span = new Span(spanContext, DateTimeOffset.UtcNow, new OpenTelemetryTags());
            using var scope = new Scope(parent: null, span, new AsyncLocalScopeManager(), finishOnClose: true);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span, openTelemetrySemanticsEnabled: true);

            span.GetTag(Tags.HttpStatusCode).Should().BeNull();
            span.GetTag(key).Should().Be(statusCode.ToString());
        }
    }
}
