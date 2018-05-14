using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Moq;
using OpenTracing;
using OpenTracing.Propagation;
using Xunit;

namespace Datadog.Trace.OpenTracing.Tests
{
    public class OpenTracingTracerTests
    {
        private OpenTracingTracer _tracer;

        public OpenTracingTracerTests()
        {
            var writerMock = new Mock<IAgentWriter>();
            var datadogTracer = new Tracer(writerMock.Object);
            _tracer = new OpenTracingTracer(datadogTracer);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            var builder = _tracer.BuildSpan("Op1");
            var span = (OpenTracingSpan)builder.Start();

            Assert.Equal("testhost", span.DDSpan.ServiceName);
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

            Assert.Equal(rootDatadogSpan.TraceContext, childDatadogSpan.TraceContext);
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

            Assert.Same(rootDatadogSpan.TraceContext, child1DatadogSpan.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Same(rootDatadogSpan.TraceContext, child2DatadogSpan.TraceContext);
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

            Assert.Same(rootDatadogSpan.TraceContext, child1DatadogSpan.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, child1DatadogSpan.Context.ParentId);
            Assert.Same(rootDatadogSpan.TraceContext, child2DatadogSpan.TraceContext);
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

            Assert.Equal(rootDatadogSpan.TraceContext, syncChild.DDSpan.TraceContext);
            Assert.Equal(rootDatadogSpan.Context.SpanId, syncChild.DDSpan.Context.ParentId);

            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(rootDatadogSpan.TraceContext, span.DDSpan.TraceContext);
                Assert.Equal(rootDatadogSpan.Context.SpanId, span.DDSpan.Context.ParentId);
            }
        }

        [Fact]
        public void Inject_HttpHeadersFormat_CorrectHeaders()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(span.DDSpan.Context.TraceId.ToString(), headers.Get(HttpHeaderNames.HttpHeaderTraceId));
            Assert.Equal(span.DDSpan.Context.SpanId.ToString(), headers.Get(HttpHeaderNames.HttpHeaderParentId));
        }

        [Fact]
        public void Extract_HeadersProperlySet_SpanContext()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            var headers = new MockTextMap();
            headers.Set(HttpHeaderNames.HttpHeaderParentId, parentId.ToString());
            headers.Set(HttpHeaderNames.HttpHeaderTraceId, traceId.ToString());

            var otSpanContext = (OpenTracingSpanContext)_tracer.Extract(BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(parentId, otSpanContext.Context.SpanId);
            Assert.Equal(traceId, otSpanContext.Context.TraceId);
        }
    }
}
