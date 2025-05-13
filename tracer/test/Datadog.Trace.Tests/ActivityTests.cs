// <copyright file="ActivityTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Propagators;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests
{
#if NETCOREAPP2_1
    // These tests triggers a Jit bug in ARM64 version of NETCOREAPP2.1
    // We don't support ARM64 < netcoreapp3.1 so we skip these tests for not being required.
    [Trait("Category", "ArmUnsupported")]
#endif
    [Collection(nameof(ActivityTestsCollection))]
    [TracerRestorer]
    public class ActivityTests : IClassFixture<ActivityTests.ActivityFixture>
    {
        private readonly ActivityFixture _fixture;

        public ActivityTests(ActivityFixture fixture)
        {
            _fixture = fixture;
        }

        [SkippableFact]
        public void SimpleActivitiesAndSpansTest()
        {
            // macos 12 is crazy flaky around timings
            // We should unskip this once we have resolved the issues around Hierarchical IDs
            SkipOn.Platform(SkipOn.PlatformValue.MacOs);

            var settings = new TracerSettings();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            Tracer.Instance.ActiveScope.Should().BeNull();

            SD.Activity myActivity = null;
            IScope scopeFromActivity = null;
            try
            {
                // Create activity as a base of the trace
                myActivity = new SD.Activity("Custom activity");

                // Set some tags before start
                myActivity.AddTag("BeforeStartTag", "MyValue");

                // Start activity
                _fixture.StartActivity(myActivity);

                // Set some tags after start
                myActivity.AddTag("AfterStartTag", "MyValue");

                // An activity should create a new datadog scope
                Tracer.Instance.ActiveScope.Should().NotBeNull();

                // Extract values for assertions
                var traceId = myActivity.TraceId.ToString();
                var spanId = myActivity.SpanId.ToString();
                HexString.TryParseUInt64(traceId.Substring(16), out var traceIdInULong);
                HexString.TryParseUInt64(spanId, out var spanIdInULong);

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
                    SD.Activity childActivity = null;
                    IScope scopeFromChildActivity = null;
                    try
                    {
                        childActivity = new SD.Activity("Child activity");
                        childActivity.AddTag("BeforeStartTag", "MyValue");
                        _fixture.StartActivity(childActivity);
                        childActivity.AddTag("AfterStartTag", "MyValue");

                        // An activity should create a new datadog scope
                        Tracer.Instance.ActiveScope.Should().NotBeNull();

                        // Assert trace id and parent span id
                        childActivity.TraceId.ToString().Should().Be(traceId);
                        childActivity.ParentSpanId.ToString().Should().Be(scope.Span.SpanId.ToString("x16"));

                        // Assert scope created from activity
                        scopeFromChildActivity = Tracer.Instance.ActiveScope;
                        scopeFromChildActivity.Span.TraceId.Should().Be(traceIdInULong);
                        HexString.TryParseUInt64(childActivity.SpanId.ToString(), out var childActivitySpanId);
                        scopeFromChildActivity.Span.SpanId.Should().Be(childActivitySpanId);
                        ((Span)scopeFromChildActivity.Span).Context.RawTraceId.Should().Be(traceId);
                        scopeFromChildActivity.Span.OperationName.Should().Be("Child activity");

                        Tracer.Instance.ActiveScope.Should().NotBe(scope);
                    }
                    finally
                    {
                        _fixture.StopActivity(childActivity);

                        // Tags are copied on activity close.
                        scopeFromChildActivity?.Span.GetTag("BeforeStartTag").Should().Be("MyValue");
                        scopeFromChildActivity?.Span.GetTag("AfterStartTag").Should().Be("MyValue");
                    }

                    Tracer.Instance.ActiveScope.Should().Be(scope);
                }
            }
            finally
            {
                _fixture.StopActivity(myActivity);

                // Tags are copied on activity close.
                scopeFromActivity?.Span.GetTag("BeforeStartTag").Should().Be("MyValue");
                scopeFromActivity?.Span.GetTag("AfterStartTag").Should().Be("MyValue");
            }

            Tracer.Instance.ActiveScope.Should().BeNull();
        }

        [Fact]
        public void SimpleSpansAndActivitiesTest()
        {
            var settings = new TracerSettings();
            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            Tracer.UnsafeSetTracerInstance(tracer);

            Tracer.Instance.ActiveScope.Should().BeNull();

            // Create datadog span as a child
            using (var scope = Tracer.Instance.StartActive("Root operation"))
            {
                Tracer.Instance.ActiveScope.Should().Be(scope);

                var traceId = scope.Span.TraceId;
                var spanId = scope.Span.SpanId;

                var hexTraceId = traceId.ToString("x32");
                var hexSpanId = spanId.ToString("x16");

                var traceId128 = (scope.Span as Span)?.TraceId128.ToString();

                // Create a new child activity as child of span
                SD.Activity childActivity = null;
                try
                {
                    childActivity = new SD.Activity("Child activity");
                    _fixture.StartActivity(childActivity);

                    // An activity should create a new datadog scope
                    Tracer.Instance.ActiveScope.Should().NotBe(scope);

                    // Assert trace id and parent span id
                    childActivity.TraceId.ToString().Should().Be(traceId128);
                    childActivity.ParentSpanId.ToString().Should().Be(hexSpanId);
                    HexString.TryParseUInt64(childActivity.SpanId.ToString(), out var spanIdInULong);

                    // Assert scope created from activity
                    var scopeFromChildActivity = Tracer.Instance.ActiveScope;
                    scopeFromChildActivity.Span.TraceId.Should().Be(traceId);
                    scopeFromChildActivity.Span.SpanId.Should().Be(spanIdInULong);
                    ((Span)scopeFromChildActivity.Span).Context.RawTraceId.Should().Be(traceId128);
                    scopeFromChildActivity.Span.OperationName.Should().Be("Child activity");

                    // Create datadog span as a child
                    using (var childScope = Tracer.Instance.StartActive("New operation"))
                    {
                        // Assert TraceId and parent span id
                        childScope.Span.TraceId.Should().Be(traceId);
                        ((Span)childScope.Span).Context.ParentId.Should().Be(spanIdInULong);
                        ((Span)childScope.Span).Context.RawTraceId.Should().Be(traceId128);
                    }
                }
                finally
                {
                    _fixture.StopActivity(childActivity);
                }

                Tracer.Instance.ActiveScope.Should().Be(scope);
            }

            Tracer.Instance.ActiveScope.Should().BeNull();
        }

        public class ActivityFixture : IDisposable
        {
#if (NETCOREAPP2_0_OR_GREATER || NETFRAMEWORK) && !NET5_0_OR_GREATER
            private readonly SD.DiagnosticSource source = new SD.DiagnosticListener("ActivityFixture");
#endif

            public ActivityFixture()
            {
                Activity.ActivityListener.Initialize();
            }

            public void Dispose()
            {
                Activity.ActivityListener.StopListeners();
            }

            public void StartActivity(SD.Activity activity)
            {
                if (activity is null)
                {
                    return;
                }

#if (NETCOREAPP2_0_OR_GREATER || NETFRAMEWORK) && !NET5_0_OR_GREATER
                source.StartActivity(activity, null);
#else
                activity.Start();
#endif
            }

            public void StopActivity(SD.Activity activity)
            {
                if (activity is null)
                {
                    return;
                }

#if (NETCOREAPP2_0_OR_GREATER || NETFRAMEWORK) && !NET5_0_OR_GREATER
                source.StopActivity(activity, null);
#else
                activity.Stop();
#endif
            }
        }
    }
}
