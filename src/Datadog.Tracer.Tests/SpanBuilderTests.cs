using OpenTracing;
using System;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SpanBuilderTests
    {
        [Fact]
        public void Start_NoParentProvided_RootSpan()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var span = spanBuilder.Start();
            var spanContext = (SpanContext)span.Context;
            Assert.Null(spanContext.ParentId);
            Assert.NotEqual<ulong>(0, spanContext.SpanId);
            Assert.NotEqual<ulong>(0, spanContext.TraceId);
        }

        [Fact]
        public void Start_AsChildOfSpan_ChildReferencesParent()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var root = spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            spanBuilder = new SpanBuilder(null, null);
            spanBuilder.AsChildOf(root);
            var child = spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            Assert.Null(rootContext.ParentId);
            Assert.NotEqual<ulong>(0, rootContext.SpanId);
            Assert.NotEqual<ulong>(0, rootContext.TraceId);
            Assert.Equal(rootContext.SpanId, childContext.ParentId);
            Assert.Equal(rootContext.TraceId, childContext.TraceId);
            Assert.NotEqual<ulong>(0, childContext.SpanId);
        }

        [Fact]
        public void Start_AsChildOfSpanContext_ChildReferencesParent()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var root = spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            spanBuilder = new SpanBuilder(null, null);
            spanBuilder.AsChildOf(rootContext);
            var child = spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            Assert.Null(rootContext.ParentId);
            Assert.NotEqual<ulong>(0, rootContext.SpanId);
            Assert.NotEqual<ulong>(0, rootContext.TraceId);
            Assert.Equal(rootContext.SpanId, childContext.ParentId);
            Assert.Equal(rootContext.TraceId, childContext.TraceId);
            Assert.NotEqual<ulong>(0, childContext.SpanId);
        }

        [Fact]
        public void Start_ReferenceAsChildOf_ChildReferencesParent()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var root = spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            spanBuilder = new SpanBuilder(null, null);
            spanBuilder.AddReference(References.ChildOf, rootContext);
            var child = spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            Assert.Null(rootContext.ParentId);
            Assert.NotEqual<ulong>(0, rootContext.SpanId);
            Assert.NotEqual<ulong>(0, rootContext.TraceId);
            Assert.Equal(rootContext.SpanId, childContext.ParentId);
            Assert.Equal(rootContext.TraceId, childContext.TraceId);
            Assert.NotEqual<ulong>(0, childContext.SpanId);
        }

        [Fact]
        public void Start_SetStartTimeStamp_TimeStampIsProperlySet()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var root = spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            spanBuilder = new SpanBuilder(null, null);
            spanBuilder.AddReference(References.ChildOf, rootContext);

            var child = spanBuilder.Start();

            var childContext = (SpanContext)child.Context;
            Assert.Null(rootContext.ParentId);
            Assert.NotEqual<ulong>(0, rootContext.SpanId);
            Assert.NotEqual<ulong>(0, rootContext.TraceId);
            Assert.Equal(rootContext.SpanId, childContext.ParentId);
            Assert.Equal(rootContext.TraceId, childContext.TraceId);
            Assert.NotEqual<ulong>(0, childContext.SpanId);
        }

        [Fact]
        public void Start_WithTags_TagsAreProperlySet()
        {
            var spanBuilder = new SpanBuilder(null, null);
            spanBuilder.WithTag("StringKey", "What's tracing");
            spanBuilder.WithTag("IntKey", 42);
            spanBuilder.WithTag("DoubleKey", 1.618);
            spanBuilder.WithTag("BoolKey", true);

            var span = (Span)spanBuilder.Start();

            Assert.Equal("What's tracing", span.GetTag("StringKey"));
            Assert.Equal("42", span.GetTag("IntKey"));
            Assert.Equal("1.618", span.GetTag("DoubleKey"));
            Assert.Equal("True", span.GetTag("BoolKey"));
        }

        [Fact]
        public void Start_SettingServiceAndResource_ServiceAndResourceAreSet()
        {
            var spanBuilder = new SpanBuilder(null, null);
            spanBuilder.WithTag(Tags.Service, "MyService");
            spanBuilder.WithTag(Tags.Resource, "MyResource");

            var span = (Span)spanBuilder.Start();

            Assert.Equal("MyService", span.ServiceName);
            Assert.Equal("MyResource", span.ResourceName);
        }

        [Fact]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var spanBuilder = new SpanBuilder(null, null);
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            spanBuilder.WithStartTimestamp(startTime);

            var span = (Span)spanBuilder.Start();

            Assert.Equal(startTime, span.StartTime);
        }

        [Fact]
        public void Start_SetOperationName_OperationNameProperlySet()
        {
            var spanBuilder = new SpanBuilder(null, "Op1");

            var span = (Span)spanBuilder.Start();

            Assert.Equal("Op1", span.OperationName);
        }
    }
}
