using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OpenTracing.Propagation;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class OpenTracingTracerTests
    {
        private OpenTracingTracer _tracer;

        public OpenTracingTracerTests()
        {
            var mockAgentWriter = new Mock<IAgentWriter>();
            var ddTracer = new Tracer(mockAgentWriter.Object);
           _tracer = new OpenTracingTracer(ddTracer);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            string currentDomainFriendlyName = AppDomain.CurrentDomain.FriendlyName;
            var builder = _tracer.BuildSpan("Op1");
            var span = (OpenTracingSpan)builder.Start();

            Assert.Equal(currentDomainFriendlyName, span.DatadogSpan.ServiceName);
            Assert.Equal("Op1", span.DatadogSpan.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChild_ChildParentProperlySet()
        {
            var root = (OpenTracingScope)_tracer
                                         .BuildSpan("Root")
                                         .StartActive(finishSpanOnDispose: true);

            var child = (OpenTracingScope)_tracer
                                          .BuildSpan("Child")
                                          .StartActive(finishSpanOnDispose: true);

            Assert.Equal(root.Span.DatadogSpan.TraceContext, child.Span.DatadogSpan.TraceContext);
            Assert.Equal(root.Span.DatadogSpan.Context.SpanId, child.Span.DatadogSpan.Context.ParentId);
        }

        [Fact]
        public void BuildScope_2ChildrenOfRoot_ChildrenParentProperlySetIfFinished()
        {
            var root = _tracer
                       .BuildSpan("Root")
                       .StartActive(finishSpanOnDispose: true);

            Assert.Same(root, _tracer.ActiveSpan);

            var child1 = _tracer
                         .BuildSpan("Child1")
                         .StartActive(finishSpanOnDispose: true);

            Assert.Same(child1, _tracer.ActiveSpan);

            child1.Span.Finish();

            Assert.Same(root, _tracer.ActiveSpan);

            var child2 = _tracer
                         .BuildSpan("Child2")
                         .StartActive(finishSpanOnDispose: true);

            Assert.Same(child2, _tracer.ActiveSpan);

            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.TraceContext, ((OpenTracingScope)child1).DatadogScope.Span.TraceContext);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.Context.SpanId, ((OpenTracingScope)child1).DatadogScope.Span.Context.ParentId);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.TraceContext, ((OpenTracingScope)child2).DatadogScope.Span.TraceContext);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.Context.SpanId, ((OpenTracingScope)child2).DatadogScope.Span.Context.ParentId);
        }

        [Fact]
        public void BuildScope_2ChildrenOfRoot_ChildrenParentProperlySetIfDisposed()
        {
            var root = _tracer
                       .BuildSpan("Root")
                       .StartActive(finishSpanOnDispose: true);

            Assert.Same(root, _tracer.ActiveSpan);

            var child1 = _tracer
                         .BuildSpan("Child1")
                         .StartActive(finishSpanOnDispose: true);

            Assert.Same(child1, _tracer.ActiveSpan);

            child1.Dispose();

            Assert.Same(root, _tracer.ActiveSpan);

            var child2 = _tracer
                         .BuildSpan("Child2")
                         .StartActive(finishSpanOnDispose: true);

            Assert.Same(child2, _tracer.ActiveSpan);

            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.TraceContext, ((OpenTracingScope)child1).DatadogScope.Span.TraceContext);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.Context.SpanId, ((OpenTracingScope)child1).DatadogScope.Span.Context.ParentId);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.TraceContext, ((OpenTracingScope)child2).DatadogScope.Span.TraceContext);
            Assert.Equal(((OpenTracingScope)root).DatadogScope.Span.Context.SpanId, ((OpenTracingScope)child2).DatadogScope.Span.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
        {
            var root = (OpenTracingScope)_tracer
                                         .BuildSpan("Root")
                                         .StartActive(finishSpanOnDispose: true);
            var child1 = (OpenTracingScope)_tracer
                                           .BuildSpan("Child1")
                                           .StartActive(finishSpanOnDispose: true);
            var child2 = (OpenTracingScope)_tracer
                                           .BuildSpan("Child2")
                                           .StartActive(finishSpanOnDispose: true);

            Assert.Same(root.DatadogScope, child1.DatadogScope.Parent);
            Assert.Same(child1.DatadogScope, child2.DatadogScope.Parent);

            Assert.Equal(root.Span.DatadogSpan.TraceContext, child1.Span.DatadogSpan.TraceContext);
            Assert.Equal(root.Span.DatadogSpan.Context.SpanId, child1.Span.DatadogSpan.Context.ParentId);
            Assert.Equal(root.Span.DatadogSpan.TraceContext, child2.Span.DatadogSpan.TraceContext);
            Assert.Equal(child1.Span.DatadogSpan.Context.SpanId, child2.Span.DatadogSpan.Context.ParentId);
        }

        [Fact]
        public async Task BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            var root = (OpenTracingSpan)_tracer
                .BuildSpan("Root")
                .Start();

            Func<OpenTracingTracer, Task<OpenTracingSpan>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return (OpenTracingSpan)_tracer.BuildSpan("AsyncChild").Start();
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = (OpenTracingSpan)_tracer.BuildSpan("SyncChild").Start();
            tcs.SetResult(true);

            Assert.Equal(root.DatadogSpan.TraceContext, syncChild.DatadogSpan.TraceContext);
            Assert.Equal(root.DatadogSpan.Context.SpanId, syncChild.DatadogSpan.Context.ParentId);
            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(root.DatadogSpan.TraceContext, span.DatadogSpan.TraceContext);
                Assert.Equal(root.DatadogSpan.Context.SpanId, span.DatadogSpan.Context.ParentId);
            }
        }

        [Fact]
        public void Inject_HttpHeadersFormat_CorrectHeaders()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(span.DatadogSpan.Context.TraceId.ToString(), headers.Get(Constants.HttpHeaderTraceId));
            Assert.Equal(span.DatadogSpan.Context.SpanId.ToString(), headers.Get(Constants.HttpHeaderParentId));
        }

        [Fact]
        public void Extract_HeadersProperlySet_SpanContext()
        {
            const ulong parentId = 10;
            const ulong traceId = 42;
            var headers = new MockTextMap();
            headers.Set(Constants.HttpHeaderParentId, parentId.ToString());
            headers.Set(Constants.HttpHeaderTraceId, traceId.ToString());

            var context = (SpanContext)_tracer.Extract(BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(parentId, context.SpanId);
            Assert.Equal(traceId, context.TraceId);
        }
    }
}
