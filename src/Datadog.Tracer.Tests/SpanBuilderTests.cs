using Moq;
using OpenTracing;
using System;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class SpanBuilderTests
    {
        private Mock<IDatadogTracer> _tracerMock;
        private Mock<ITraceContext> _traceContextMock;
        private SpanBuilder _spanBuilder;
        private const string _defaultServiceName = "DefaultServiceName";

        public SpanBuilderTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            _traceContextMock = new Mock<ITraceContext>(MockBehavior.Strict);
            _traceContextMock.Setup(x => x.AddSpan(It.IsAny<Span>()));
            _tracerMock.Setup(x => x.GetTraceContext()).Returns(_traceContextMock.Object);
            _tracerMock.Setup(x => x.DefaultServiceName).Returns(_defaultServiceName);
            _spanBuilder = new SpanBuilder(_tracerMock.Object, null);
        }

        [Fact]
        public void Start_NoServiceName_DefaultServiceNameIsSet()
        {
            var span = (Span)_spanBuilder.Start();

            Assert.Equal(_defaultServiceName, span.ServiceName);
        }

        [Fact]
        public void Start_NoParentProvided_RootSpan()
        {
            var span = _spanBuilder.Start();
            var spanContext = (SpanContext)span.Context;

            _traceContextMock.Verify(x => x.AddSpan(It.IsAny<Span>()), Times.Once);
            Assert.Null(spanContext.ParentId);
            Assert.NotEqual<ulong>(0, spanContext.SpanId);
            Assert.NotEqual<ulong>(0, spanContext.TraceId);
        }

        [Fact]
        public void Start_AsChildOfSpan_ChildReferencesParent()
        {
            var root = _spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            _spanBuilder = new SpanBuilder(null, null);
            _spanBuilder.AsChildOf(root);
            var child = _spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            _traceContextMock.Verify(x => x.AddSpan(It.IsAny<Span>()), Times.Exactly(2));
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
            var root = _spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            _spanBuilder = new SpanBuilder(null, null);
            _spanBuilder.AsChildOf(rootContext);
            var child = _spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            _traceContextMock.Verify(x => x.AddSpan(It.IsAny<Span>()), Times.Exactly(2));
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
            var root = _spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            _spanBuilder = new SpanBuilder(null, null);
            _spanBuilder.AddReference(References.ChildOf, rootContext);
            var child = _spanBuilder.Start();
            var childContext = (SpanContext)child.Context;

            _traceContextMock.Verify(x => x.AddSpan(It.IsAny<Span>()), Times.Exactly(2));
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
            var root = _spanBuilder.Start();
            var rootContext = (SpanContext)root.Context;
            _spanBuilder = new SpanBuilder(null, null);
            _spanBuilder.AddReference(References.ChildOf, rootContext);

            var child = _spanBuilder.Start();

            _traceContextMock.Verify(x => x.AddSpan(It.IsAny<Span>()), Times.Exactly(2));
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
            _spanBuilder.WithTag("StringKey", "What's tracing");
            _spanBuilder.WithTag("IntKey", 42);
            _spanBuilder.WithTag("DoubleKey", 1.618);
            _spanBuilder.WithTag("BoolKey", true);

            var span = (Span)_spanBuilder.Start();

            Assert.Equal("What's tracing", span.GetTag("StringKey"));
            Assert.Equal("42", span.GetTag("IntKey"));
            Assert.Equal("1.618", span.GetTag("DoubleKey"));
            Assert.Equal("True", span.GetTag("BoolKey"));
        }

        [Fact]
        public void Start_SettingService_ServiceIsSet()
        {
            _spanBuilder.WithTag(Tags.Service, "MyService");

            var span = (Span)_spanBuilder.Start();

            Assert.Equal("MyService", span.ServiceName);
        }

        [Fact]
        public void Start_SettingResource_ResourceIsSet()
        {
            _spanBuilder.WithTag(Tags.Resource, "MyResource");

            var span = (Span)_spanBuilder.Start();

            Assert.Equal("MyResource", span.ResourceName);
        }

        [Fact]
        public void Start_SettingType_TypeIsSet()
        {
            _spanBuilder.WithTag(Tags.Type, "web");

            var span = (Span)_spanBuilder.Start();

            Assert.Equal("web", span.Type);
        }

        [Fact]
        public void Start_SettingError_ErrorIsSet()
        {
            _spanBuilder.WithTag(Tags.Error, true);

            var span = (Span)_spanBuilder.Start();

            Assert.Equal(true, span.Error);
        }

        [Fact]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            _spanBuilder.WithStartTimestamp(startTime);

            var span = (Span)_spanBuilder.Start();

            Assert.Equal(startTime, span.StartTime);
        }

        [Fact]
        public void Start_SetOperationName_OperationNameProperlySet()
        {
            var spanBuilder = new SpanBuilder(_tracerMock.Object, "Op1");

            var span = (Span)spanBuilder.Start();

            Assert.Equal("Op1", span.OperationName);
        }
    }
}
