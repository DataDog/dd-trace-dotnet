// <copyright file="OpenTelemetrySpecialTagRemapperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(OpenTelemetrySpecialTagRemapperTests))]
    public class OpenTelemetrySpecialTagRemapperTests
    {
        [Fact]
        public void OperationName_Tag_Should_Override_OperationName()
        {
            var expected = "overridden.name";
            var activityMock = new Mock<IActivity5>();
            activityMock.Setup(x => x.Kind).Returns(ActivityKind.Server);
            var tagObjects = new Dictionary<string, object>
            {
                { "http.request.method", "GET" },
                { "operation.name", expected }
            };
            activityMock.Setup(x => x.TagObjects).Returns(tagObjects);

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
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

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
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

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
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

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.Type);
        }

        [Theory]
        [InlineData("true", 1.0)]
        [InlineData("false", 0.0)]
        public void AnalyticsEvent_Tag_Should_Override_AnalyticsSamplingRateMetric(string value, double expected)
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

            var span = new Span(new SpanContext(1, 1), DateTimeOffset.UtcNow);
            OtlpHelpers.UpdateSpanFromActivity(activityMock.Object, span);

            Assert.Equal(expected, span.Type);
        }
    }
}
