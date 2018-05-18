using System;
using Datadog.Trace.Agent;
using Moq;
using OpenTracing;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingSpanBuilderTests
    {
        private static readonly string DefaultServiceName = $"{nameof(OpenTracingSpanBuilderTests)}";

        private OpenTracingTracer _tracer;

        public OpenTracingSpanBuilderTests()
        {
            var writerMock = new Mock<IAgentWriter>(MockBehavior.Strict);
            var datadogTracer = new Tracer(writerMock.Object, defaultServiceName: DefaultServiceName);
            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Fact]
        public void Start_NoServiceName_DefaultServiceNameIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null).Start();

            Assert.Equal(DefaultServiceName, span.DDSpan.ServiceName);
        }

        [Fact]
        public void Start_NoParentProvided_RootSpan()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            SpanContext ddSpanContext = span.Context.Context;

            Assert.Null(ddSpanContext.ParentId);
            Assert.NotEqual<ulong>(0, ddSpanContext.SpanId);
            Assert.NotEqual<ulong>(0, ddSpanContext.TraceId);
        }

        [Fact]
        public void Start_AsChildOfSpan_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                .AsChildOf(root)
                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.TraceId);
            Assert.Equal(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.Equal(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DDSpan.Context.SpanId);
        }

        [Fact]
        public void Start_AsChildOfSpanContext_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                .AsChildOf(root.Context)
                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.TraceId);
            Assert.Equal(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.Equal(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DDSpan.Context.SpanId);
        }

        [Fact]
        public void Start_ReferenceAsChildOf_ChildReferencesParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null).Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                .AddReference(References.ChildOf, root.Context)
                .Start();

            Assert.Null(root.DDSpan.Context.ParentId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.SpanId);
            Assert.NotEqual<ulong>(0, root.DDSpan.Context.TraceId);
            Assert.Equal(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
            Assert.Equal(root.DDSpan.Context.TraceId, child.DDSpan.Context.TraceId);
            Assert.NotEqual<ulong>(0, child.DDSpan.Context.SpanId);
        }

        [Fact]
        public void Start_WithTags_TagsAreProperlySet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                .WithTag("StringKey", "What's tracing")
                .WithTag("IntKey", 42)
                .WithTag("DoubleKey", 1.618)
                .WithTag("BoolKey", true)
                .Start();

            Assert.Equal("What's tracing", span.DDSpan.GetTag("StringKey"));
            Assert.Equal("42", span.DDSpan.GetTag("IntKey"));
            Assert.Equal("1.618", span.DDSpan.GetTag("DoubleKey"));
            Assert.Equal("True", span.DDSpan.GetTag("BoolKey"));
        }

        [Fact]
        public void Start_SettingService_ServiceIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                 .WithTag(DatadogTags.ServiceName, "MyService")
                 .Start();

            Assert.Equal("MyService", span.DDSpan.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInParent_ImplicitChildInheritServiceName()
        {
            IScope root = _tracer.BuildSpan(null)
                 .WithTag(DatadogTags.ServiceName, "MyService")
                 .StartActive(finishSpanOnDispose: true);
            IScope child = _tracer.BuildSpan(null)
                 .StartActive(finishSpanOnDispose: true);

            Assert.Equal("MyService", ((OpenTracingSpan)root.Span).Span.ServiceName);
            Assert.Equal("MyService", ((OpenTracingSpan)child.Span).Span.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInParent_ExplicitChildInheritServiceName()
        {
            IScope root = _tracer.BuildSpan(null)
                .WithTag(DatadogTags.ServiceName, "MyService")
                .StartActive(finishSpanOnDispose: true);
            IScope child = _tracer.BuildSpan(null)
                .AsChildOf(root.Span)
                .StartActive(finishSpanOnDispose: true);

            Assert.Equal("MyService", ((OpenTracingSpan)root.Span).Span.ServiceName);
            Assert.Equal("MyService", ((OpenTracingSpan)child.Span).Span.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInParent_NotChildDontInheritServiceName()
        {
            ISpan span1 = _tracer.BuildSpan(null)
                .WithTag(DatadogTags.ServiceName, "MyService")
                .Start();
            IScope root = _tracer.BuildSpan(null)
                .StartActive(finishSpanOnDispose: true);

            Assert.Equal("MyService", ((OpenTracingSpan)span1).Span.ServiceName);
            Assert.Equal("OpenTracingSpanBuilderTests", ((OpenTracingSpan)root.Span).Span.ServiceName);
        }

        [Fact]
        public void Start_SettingServiceInChild_ServiceNameOverrideParent()
        {
            var root = (OpenTracingSpan)_tracer.BuildSpan(null)
                 .WithTag(DatadogTags.ServiceName, "MyService")
                 .Start();
            var child = (OpenTracingSpan)_tracer.BuildSpan(null)
                 .WithTag(DatadogTags.ServiceName, "AnotherService")
                 .Start();

            Assert.Equal("MyService", root.DDSpan.ServiceName);
            Assert.Equal("AnotherService", child.DDSpan.ServiceName);
        }

        [Fact]
        public void Start_SettingResource_ResourceIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                .WithTag("resource.name", "MyResource")
                .Start();

            Assert.Equal("MyResource", span.DDSpan.ResourceName);
        }

        [Fact]
        public void Start_SettingType_TypeIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                .WithTag("span.type", "web")
                .Start();

            Assert.Equal("web", span.DDSpan.Type);
        }

        [Fact]
        public void Start_SettingError_ErrorIsSet()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                .WithTag(global::OpenTracing.Tag.Tags.Error.Key, true)
                .Start();

            Assert.True(span.DDSpan.Error);
        }

        [Fact]
        public void Start_WithStartTimeStamp_TimeStampProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var span = (OpenTracingSpan)_tracer.BuildSpan(null)
                .WithStartTimestamp(startTime)
                .Start();

            Assert.Equal(startTime, span.DDSpan.StartTime);
        }

        [Fact]
        public void Start_SetOperationName_OperationNameProperlySet()
        {
            var spanBuilder = new OpenTracingSpanBuilder(_tracer, "Op1");

            var span = (OpenTracingSpan)spanBuilder.Start();

            Assert.Equal("Op1", span.DDSpan.OperationName);
        }
    }
}
