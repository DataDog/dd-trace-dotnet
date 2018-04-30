using System;
using Moq;
using OpenTracing;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class OpenTracingSpanBuilderTests
    {
        private const string _defaultServiceName = "DefaultServiceName";

        private Mock<IAgentWriter> _writerMock;
        private Tracer _tracer;
        private Func<OpenTracingSpanBuilder> _createSpanBuilder;

        public OpenTracingSpanBuilderTests()
        {
            _writerMock = new Mock<IAgentWriter>(MockBehavior.Strict);
            _tracer = new Tracer(_writerMock.Object, defaultServiceName: _defaultServiceName);
            _createSpanBuilder = () => new OpenTracingSpanBuilder(_tracer, null);
        }

        [Fact]
        public void Start_NoServiceName_DefaultServiceNameIsSet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder().Start();

            Assert.Equal(_defaultServiceName, span.DatadogSpan.ServiceName);
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
            var root = (OpenTracingSpan)_createSpanBuilder().Start();
            var child = (OpenTracingSpan)_createSpanBuilder()
                .AsChildOf(root)
                .Start();

            Assert.Null(root.DatadogSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.TraceId);
            Assert.Equal(root.DatadogSpan.Context.SpanId, child.DatadogSpan.Context.ParentId);
            Assert.Equal(root.DatadogSpan.Context.TraceId, child.DatadogSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DatadogSpan.Context.SpanId);
        }

        [Fact]
        public void Start_AsChildOfSpanContext_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_createSpanBuilder().Start();
            var child = (OpenTracingSpan)_createSpanBuilder()
                .AsChildOf(root.Context)
                .Start();

            Assert.Null(root.DatadogSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.TraceId);
            Assert.Equal(root.DatadogSpan.Context.SpanId, child.DatadogSpan.Context.ParentId);
            Assert.Equal(root.DatadogSpan.Context.TraceId, child.DatadogSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DatadogSpan.Context.SpanId);
        }

        [Fact]
        public void Start_ReferenceAsChildOf_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_createSpanBuilder().Start();
            var child = (OpenTracingSpan)_createSpanBuilder()
                .AddReference(References.ChildOf, root.Context)
                .Start();

            Assert.Null(root.DatadogSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DatadogSpan.Context.TraceId);
            Assert.Equal(root.DatadogSpan.Context.SpanId, child.DatadogSpan.Context.ParentId);
            Assert.Equal(root.DatadogSpan.Context.TraceId, child.DatadogSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DatadogSpan.Context.SpanId);
        }

        [Fact]
        public void Start_WithTags_TagsAreProperlySet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder()
                .WithTag("StringKey", "What's tracing")
                .WithTag("IntKey", 42)
                .WithTag("DoubleKey", 1.618)
                .WithTag("BoolKey", true)
                .Start();

            Assert.Equal("What's tracing", span.DatadogSpan.GetTag("StringKey"));
            Assert.Equal("42", span.DatadogSpan.GetTag("IntKey"));
            Assert.Equal("1.618", span.DatadogSpan.GetTag("DoubleKey"));
            Assert.Equal("True", span.DatadogSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void Start_SettingService_ServiceIsSet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();

            Assert.Equal("MyService", span.DatadogSpan.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInParent_ChildInheritServiceName()
        {
            var root = (OpenTracingSpan)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();
            var child = (OpenTracingSpan)_createSpanBuilder()
                 .Start();

            Assert.Equal("MyService", root.DatadogSpan.ServiceName);
            Assert.Equal("MyService", child.DatadogSpan.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInChild_ServiceNameOverrideParent()
        {
            var root = (OpenTracingSpan)_createSpanBuilder()
                 .WithTag("service.name", "MyService")
                 .Start();
            var child = (OpenTracingSpan)_createSpanBuilder()
                 .WithTag("service.name", "AnotherService")
                 .Start();

            Assert.Equal("MyService", root.DatadogSpan.ServiceName);
            Assert.Equal("AnotherService", child.DatadogSpan.ServiceName);
        }

        [Fact]
        public void Start_SettingResource_ResourceIsSet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder()
                .WithTag("resource.name", "MyResource")
                .Start();

            Assert.Equal("MyResource", span.DatadogSpan.ResourceName);
        }

        [Fact]
        public void Start_SettingType_TypeIsSet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder()
                .WithTag("span.type", "web")
                .Start();

            Assert.Equal("web", span.DatadogSpan.Type);
        }

        [Fact]
        public void Start_SettingError_ErrorIsSet()
        {
            var span = (OpenTracingSpan)_createSpanBuilder()
                .WithTag(global::OpenTracing.Tag.Tags.Error.Key, true)
                .Start();

            Assert.True(span.DatadogSpan.Error);
        }

        [Fact]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var span = (OpenTracingSpan)_createSpanBuilder()
                .WithStartTimestamp(startTime)
                .Start();

            Assert.Equal(startTime, span.DatadogSpan.StartTime);
        }

        [Fact]
        public void Start_SetOperationName_OperationNameProperlySet()
        {
            var spanBuilder = new OpenTracingSpanBuilder(_tracer, "Op1");

            var span = (OpenTracingSpan)spanBuilder.Start();

            Assert.Equal("Op1", span.DatadogSpan.OperationName);
        }
    }
}
