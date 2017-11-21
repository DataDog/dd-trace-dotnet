using System;
using Moq;
using OpenTracing;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class SpanBuilderTests
    {
        private const string _defaultServiceName = "DefaultServiceName";

        private Mock<IDatadogTracer> _tracerMock;
        private TraceContext _traceContext;
        private Func<SpanBuilder> _createSpanBuilder;

        public SpanBuilderTests()
        {
            _tracerMock = new Mock<IDatadogTracer>(MockBehavior.Strict);
            _traceContext = new TraceContext(_tracerMock.Object);
            _tracerMock.Setup(x => x.GetTraceContext()).Returns(_traceContext);
            _tracerMock.Setup(x => x.DefaultServiceName).Returns(_defaultServiceName);
            _createSpanBuilder = () => new SpanBuilder(_tracerMock.Object, null);
        }

        [Fact]
        public void Start_NoServiceName_DefaultServiceNameIsSet()
        {
            var span = (Span)_createSpanBuilder().Start();

            Assert.Equal(_defaultServiceName, span.ServiceName);
        }

        [Fact]
        public void Start_NoParentProvided_RootSpan()
        {
            var span = _createSpanBuilder().Start();
            var spanContext = (SpanContext)span.Context;

            Assert.Null(spanContext.ParentId);
            Assert.NotEqual<ulong>(0, spanContext.SpanId);
            Assert.NotEqual<ulong>(0, spanContext.TraceId);
        }

        [Fact]
        public void Start_AsChildOfSpan_ChildReferencesParent()
        {
            var root = (Span)_createSpanBuilder().Start();
            var child = (Span)_createSpanBuilder()
                .AsChildOf(root)
                .Start();

            Assert.Null(root.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.Context.TraceId);
            Assert.Equal(root.Context.SpanId, child.Context.ParentId);
            Assert.Equal(root.Context.TraceId, child.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.Context.SpanId);
        }

        [Fact]
        public void Start_AsChildOfSpanContext_ChildReferencesParent()
        {
            var root = (Span)_createSpanBuilder().Start();
            var child = (Span)_createSpanBuilder()
                .AsChildOf(root.Context)
                .Start();

            Assert.Null(root.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.Context.TraceId);
            Assert.Equal(root.Context.SpanId, child.Context.ParentId);
            Assert.Equal(root.Context.TraceId, child.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.Context.SpanId);
        }

        [Fact]
        public void Start_ReferenceAsChildOf_ChildReferencesParent()
        {
            var root = (Span)_createSpanBuilder().Start();
            var child = (Span)_createSpanBuilder()
                .AddReference(References.ChildOf, root.Context)
                .Start();

            Assert.Null(root.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.Context.TraceId);
            Assert.Equal(root.Context.SpanId, child.Context.ParentId);
            Assert.Equal(root.Context.TraceId, child.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.Context.SpanId);
        }

        [Fact]
        public void Start_WithTags_TagsAreProperlySet()
        {
            var span = (Span)_createSpanBuilder()
                .WithTag("StringKey", "What's tracing")
                .WithTag("IntKey", 42)
                .WithTag("DoubleKey", 1.618)
                .WithTag("BoolKey", true)
                .Start();

            Assert.Equal("What's tracing", span.GetTag("StringKey"));
            Assert.Equal("42", span.GetTag("IntKey"));
            Assert.Equal("1.618", span.GetTag("DoubleKey"));
            Assert.Equal("True", span.GetTag("BoolKey"));
        }

        [Fact]
        public void Start_SettingService_ServiceIsSet()
        {
            var span = (Span)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();

            Assert.Equal("MyService", span.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInParent_ChildInheritServiceName()
        {
            var root = (Span)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();
            var child = (Span)_createSpanBuilder()
                 .Start();

            Assert.Equal("MyService", root.ServiceName);
            Assert.Equal("MyService", child.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInChild_ServiceNameOverrideParent()
        {
            var root = (Span)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();
            var child = (Span)_createSpanBuilder()
                 .WithTag("service.name", "AnotherService")
                 .Start();

            Assert.Equal("MyService", root.ServiceName);
            Assert.Equal("AnotherService", child.ServiceName);
        }

        [Fact]
        public void Start_SettingResource_ResourceIsSet()
        {
            var span = (Span)_createSpanBuilder()
                .WithTag("resource.name", "MyResource")
                .Start();

            Assert.Equal("MyResource", span.ResourceName);
        }

        [Fact]
        public void Start_SettingType_TypeIsSet()
        {
            var span = (Span)_createSpanBuilder()
                .WithTag("span.type", "web")
                .Start();

            Assert.Equal("web", span.Type);
        }

        [Fact]
        public void Start_SettingError_ErrorIsSet()
        {
            var span = (Span)_createSpanBuilder()
                .WithTag(OpenTracing.Tags.Error, true)
                .Start();

            Assert.Equal(true, span.Error);
        }

        [Fact]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var span = (Span)_createSpanBuilder()
                .WithStartTimestamp(startTime)
                .Start();

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
