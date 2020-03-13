using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using OpenTracing;
using OpenTracing.Propagation;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingTracerTests
    {
        private readonly OpenTracingTracer _tracer;

        public OpenTracingTracerTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            var datadogTracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);

            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            var builder = _tracer.BuildSpan("Op1");
            var span = (OpenTracingSpan)builder.Start();

            Assert.Contains(span.DDSpan.ServiceName, TestRunners.ValidNames);
            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChild_ChildParentProperlySet()
        {
            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);
            IScope child = _tracer
                          .BuildSpan("Child")
                          .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span childDatadogSpan = ((OpenTracingSpan)child.Span).Span;

            Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)childDatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, childDatadogSpan.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            IScope child1 = _tracer
                           .BuildSpan("Child1")
                           .StartActive(finishSpanOnDispose: true);

            child1.Dispose();

            IScope child2 = _tracer
                           .BuildSpan("Child2")
                           .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span child1DatadogSpan = ((OpenTracingSpan)child1.Span).Span;
            Span child2DatadogSpan = ((OpenTracingSpan)child2.Span).Span;

            Assert.Same(rootDatadogSpan.Context.TraceContext, child1DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Same(rootDatadogSpan.Context.TraceContext, child2DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child2DatadogSpan.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
        {
            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);
            IScope child1 = _tracer
                           .BuildSpan("Child1")
                           .StartActive(finishSpanOnDispose: true);
            IScope child2 = _tracer
                           .BuildSpan("Child2")
                           .StartActive(finishSpanOnDispose: true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;
            Span child1DatadogSpan = ((OpenTracingSpan)child1.Span).Span;
            Span child2DatadogSpan = ((OpenTracingSpan)child2.Span).Span;

            Assert.Same(rootDatadogSpan.Context.TraceContext, child1DatadogSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Same(rootDatadogSpan.Context.TraceContext, child2DatadogSpan.Context.TraceContext);
            Assert.Equal(child1DatadogSpan.Context.SpanId, child2DatadogSpan.Context.ParentId);
        }

        [Fact]
        public async Task BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            IScope root = _tracer
                         .BuildSpan("Root")
                         .StartActive(finishSpanOnDispose: true);

            Func<OpenTracingTracer, Task<OpenTracingSpan>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return (OpenTracingSpan)_tracer.BuildSpan("AsyncChild").Start();
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = (OpenTracingSpan)_tracer.BuildSpan("SyncChild").Start();
            tcs.SetResult(true);

            Span rootDatadogSpan = ((OpenTracingSpan)root.Span).Span;

            Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)syncChild.DDSpan.Context.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, syncChild.DDSpan.Context.ParentId);

            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(rootDatadogSpan.Context.TraceContext, (ITraceContext)span.DDSpan.Context.TraceContext);
                Assert.Equal(rootDatadogSpan.Context.SpanId, span.DDSpan.Context.ParentId);
            }
        }

        [Fact]
        public void Inject_HttpHeadersFormat_CorrectHeaders()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(span.DDSpan.Context.TraceId.ToString(), headers.Get(HttpHeaderNames.TraceId));
            Assert.Equal(span.DDSpan.Context.SpanId.ToString(), headers.Get(HttpHeaderNames.ParentId));
        }

        [Fact]
        public void Inject_TextMapFormat_CorrectHeaders()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.TextMap, headers);

            Assert.Equal(span.DDSpan.Context.TraceId.ToString(), headers.Get(HttpHeaderNames.TraceId));
            Assert.Equal(span.DDSpan.Context.SpanId.ToString(), headers.Get(HttpHeaderNames.ParentId));
        }

        [Fact]
        public void Inject_UnknownFormat_Throws()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();
            var mockFormat = new Mock<IFormat<ITextMap>>();

            Assert.Throws<NotSupportedException>(() => _tracer.Inject(span.Context, mockFormat.Object, headers));
        }

        [Fact]
        public void Extract_HttpHeadersFormat_HeadersProperlySet_SpanContext()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderNames.ParentId, parentId.ToString());
            headers.Set(HttpHeaderNames.TraceId, traceId.ToString());

            var otSpanContext = (OpenTracingSpanContext)_tracer.Extract(BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(parentId, otSpanContext.Context.SpanId);
            Assert.Equal(traceId, otSpanContext.Context.TraceId);
        }

        [Fact]
        public void Extract_TextMapFormat_HeadersProperlySet_SpanContext()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderNames.ParentId, parentId.ToString());
            headers.Set(HttpHeaderNames.TraceId, traceId.ToString());

            var otSpanContext = (OpenTracingSpanContext)_tracer.Extract(BuiltinFormats.TextMap, headers);

            Assert.Equal(parentId, otSpanContext.Context.SpanId);
            Assert.Equal(traceId, otSpanContext.Context.TraceId);
        }

        [Fact]
        public void Extract_UnknownFormat_Throws()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderNames.ParentId, parentId.ToString());
            headers.Set(HttpHeaderNames.TraceId, traceId.ToString());
            var mockFormat = new Mock<IFormat<ITextMap>>();

            Assert.Throws<NotSupportedException>(() => _tracer.Extract(mockFormat.Object, headers));
        }

        [Fact]
        public void StartActive_NoServiceName_DefaultServiceName()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Contains(ddSpan.ServiceName, TestRunners.ValidNames);
        }

        [Fact]
        public void SetDefaultServiceName()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("DefaultServiceName", ddSpan.ServiceName);
        }

        [Fact]
        public void SetServiceName_WithTag()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                               .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void SetServiceName_SetTag()
        {
            var scope = _tracer.BuildSpan("Operation")
                               .StartActive();

            scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");
            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void OverrideDefaultServiceName_WithTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                              .StartActive();

            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void OverrideDefaultServiceName_SetTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var scope = tracer.BuildSpan("Operation")
                              .StartActive();

            scope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");
            var otSpan = (OpenTracingSpan)scope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void InheritParentServiceName_WithTag()
        {
            var parentScope = _tracer.BuildSpan("ParentOperation")
                                     .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                     .StartActive();

            var childScope = _tracer.BuildSpan("ChildOperation")
                                    .AsChildOf(parentScope.Span)
                                    .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void InheritParentServiceName_SetTag()
        {
            var parentScope = _tracer.BuildSpan("ParentOperation")
                                     .StartActive();

            parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

            var childScope = _tracer.BuildSpan("ChildOperation")
                                    .AsChildOf(parentScope.Span)
                                    .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void Parent_OverrideDefaultServiceName_WithTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var parentScope = tracer.BuildSpan("ParentOperation")
                                    .WithTag(DatadogTags.ServiceName, "MyAwesomeService")
                                    .StartActive();

            var childScope = tracer.BuildSpan("ChildOperation")
                                   .AsChildOf(parentScope.Span)
                                   .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }

        [Fact]
        public void Parent_OverrideDefaultServiceName_SetTag()
        {
            ITracer tracer = OpenTracingTracerFactory.CreateTracer(defaultServiceName: "DefaultServiceName");

            var parentScope = tracer.BuildSpan("ParentOperation")
                                    .StartActive();

            parentScope.Span.SetTag(DatadogTags.ServiceName, "MyAwesomeService");

            var childScope = tracer.BuildSpan("ChildOperation")
                                   .AsChildOf(parentScope.Span)
                                   .StartActive();

            var otSpan = (OpenTracingSpan)childScope.Span;
            var ddSpan = otSpan.Span;

            Assert.Equal("MyAwesomeService", ddSpan.ServiceName);
        }
    }
}
