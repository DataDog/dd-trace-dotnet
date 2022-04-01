// <copyright file="ActivityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using Datadog.Trace.Propagators;
using FluentAssertions;
using Xunit;
using sd = System.Diagnostics;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(ActivityTestsCollection))]
    public class ActivityTests : IClassFixture<ActivityTests.ActivityFixture>
    {
        private ActivityFixture _fixture;

        public ActivityTests(ActivityFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void SimpleActivitiesAndSpansTest()
        {
            sd.Activity.Current.Should().BeNull();
            Tracer.Instance.ActiveScope.Should().BeNull();

            sd.Activity myActivity = null;
            IScope scopeFromActivity = null;
            try
            {
                // Create activity as a base of the trace
                myActivity = new sd.Activity("Custom activity");

                // Set some tags before start
                myActivity.AddTag("BeforeStartTag", "MyValue");

                // Start activity
                myActivity.Start();

                // Set some tags after start
                myActivity.AddTag("AfterStartTag", "MyValue");

                // An activity should create a new datadog scope
                sd.Activity.Current.Should().NotBeNull();
                Tracer.Instance.ActiveScope.Should().NotBeNull();

                // Extract values for assertions
                var traceId = myActivity.TraceId.ToString();
                var spanId = myActivity.SpanId.ToString();
                var traceIdInULong = ParseUtility.ParseFromHexOrDefault(traceId.Substring(16));
                var spanIdInULong = ParseUtility.ParseFromHexOrDefault(spanId);

                // Assert scope created from activity
                scopeFromActivity = Tracer.Instance.ActiveScope;
                scopeFromActivity.Span.TraceId.Should().Be(traceIdInULong);
                scopeFromActivity.Span.SpanId.Should().Be(spanIdInULong);
                ((Span)scopeFromActivity.Span).Context.RawTraceId.Should().Be(traceId);
                scopeFromActivity.Span.OperationName.Should().Be("Custom activity");

                // Create datadog span as a child
                using (var scope = Tracer.Instance.StartActive("New operation"))
                {
                    // Assert TraceId and parent span id
                    scope.Span.TraceId.Should().Be(traceIdInULong);
                    ((Span)scope.Span).Context.ParentId.Should().Be(spanIdInULong);
                    ((Span)scope.Span).Context.RawTraceId.Should().Be(traceId);

                    // Create a new child activity as child of span
                    sd.Activity childActivity = null;
                    IScope scopeFromChildActivity = null;
                    try
                    {
                        childActivity = new sd.Activity("Child activity");
                        childActivity.AddTag("BeforeStartTag", "MyValue");
                        childActivity.Start();
                        childActivity.AddTag("AfterStartTag", "MyValue");

                        // An activity should create a new datadog scope
                        sd.Activity.Current.Should().NotBeNull();
                        Tracer.Instance.ActiveScope.Should().NotBeNull();

                        // Assert trace id and parent span id
                        childActivity.TraceId.ToString().Should().Be(traceId);
                        childActivity.ParentSpanId.ToString().Should().Be(scope.Span.SpanId.ToString("x16"));

                        // Assert scope created from activity
                        scopeFromChildActivity = Tracer.Instance.ActiveScope;
                        scopeFromChildActivity.Span.TraceId.Should().Be(traceIdInULong);
                        scopeFromChildActivity.Span.SpanId.Should().Be(ParseUtility.ParseFromHexOrDefault(childActivity.SpanId.ToString()));
                        ((Span)scopeFromChildActivity.Span).Context.RawTraceId.Should().Be(traceId);
                        scopeFromChildActivity.Span.OperationName.Should().Be("Child activity");

                        Tracer.Instance.ActiveScope.Should().NotBe(scope);
                    }
                    finally
                    {
                        childActivity?.Stop();

                        // Tags are copied on activity close.
                        scopeFromChildActivity?.Span.GetTag("BeforeStartTag").Should().Be("MyValue");
                        scopeFromChildActivity?.Span.GetTag("AfterStartTag").Should().Be("MyValue");
                    }

                    Tracer.Instance.ActiveScope.Should().Be(scope);
                }
            }
            finally
            {
                myActivity?.Stop();

                // Tags are copied on activity close.
                scopeFromActivity?.Span.GetTag("BeforeStartTag").Should().Be("MyValue");
                scopeFromActivity?.Span.GetTag("AfterStartTag").Should().Be("MyValue");
            }

            sd.Activity.Current.Should().BeNull();
            Tracer.Instance.ActiveScope.Should().BeNull();
        }

        [Fact]
        public void SimpleSpansAndActivitiesTest()
        {
            sd.Activity.Current.Should().BeNull();
            Tracer.Instance.ActiveScope.Should().BeNull();

            // Create datadog span as a child
            using (var scope = Tracer.Instance.StartActive("Root operation"))
            {
                Tracer.Instance.ActiveScope.Should().Be(scope);

                var traceId = scope.Span.TraceId;
                var spanId = scope.Span.SpanId;

                var hexTraceId = traceId.ToString("x32");
                var hexSpanId = spanId.ToString("x16");

                // Create a new child activity as child of span
                sd.Activity childActivity = null;
                try
                {
                    childActivity = new sd.Activity("Child activity");
                    childActivity.Start();

                    // An activity should create a new datadog scope
                    sd.Activity.Current.Should().NotBeNull();
                    Tracer.Instance.ActiveScope.Should().NotBe(scope);

                    // Assert trace id and parent span id
                    childActivity.TraceId.ToString().Should().Be(hexTraceId);
                    childActivity.ParentSpanId.ToString().Should().Be(hexSpanId);
                    var spanIdInULong = ParseUtility.ParseFromHexOrDefault(childActivity.SpanId.ToString());

                    // Assert scope created from activity
                    var scopeFromChildActivity = Tracer.Instance.ActiveScope;
                    scopeFromChildActivity.Span.TraceId.Should().Be(traceId);
                    scopeFromChildActivity.Span.SpanId.Should().Be(spanIdInULong);
                    ((Span)scopeFromChildActivity.Span).Context.RawTraceId.Should().Be(hexTraceId);
                    scopeFromChildActivity.Span.OperationName.Should().Be("Child activity");

                    // Create datadog span as a child
                    using (var childScope = Tracer.Instance.StartActive("New operation"))
                    {
                        // Assert TraceId and parent span id
                        childScope.Span.TraceId.Should().Be(traceId);
                        ((Span)childScope.Span).Context.ParentId.Should().Be(spanIdInULong);
                        ((Span)childScope.Span).Context.RawTraceId.Should().Be(hexTraceId);
                    }
                }
                finally
                {
                    childActivity?.Stop();
                }

                Tracer.Instance.ActiveScope.Should().Be(scope);
            }

            sd.Activity.Current.Should().BeNull();
            Tracer.Instance.ActiveScope.Should().BeNull();
        }

        public class ActivityFixture : IDisposable
        {
            public ActivityFixture()
            {
                Activity.ActivityListener.Initialize();
            }

            public void Dispose()
            {
                Activity.ActivityListener.StopListeners();
            }
        }
    }
}
#endif
