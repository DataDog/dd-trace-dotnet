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
        private Mock<IAgentWriter> _agentWriter = new Mock<IAgentWriter>();
        private OpenTracingTracer _tracer;

        public OpenTracingTracerTests()
        {
           _tracer = new OpenTracingTracer(_agentWriter.Object);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            string currentDomainFriendlyName = AppDomain.CurrentDomain.FriendlyName;
            var builder = _tracer.BuildSpan("Op1");
            var span = (OpenTracingSpan)builder.Start();

            Assert.Equal(currentDomainFriendlyName, span.DDSpan.ServiceName);
            Assert.Equal("Op1", span.DDSpan.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChild_ChildParentProperlySet()
        {
            var root = (OpenTracingSpan)_tracer
                .BuildSpan("Root")
                .Start();
            var child = (OpenTracingSpan)_tracer
                .BuildSpan("Child")
                .Start();

            Assert.Equal(root.DDSpan.TraceContext, child.DDSpan.TraceContext);
            Assert.Equal(root.DDSpan.Context.SpanId, child.DDSpan.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            var root = (OpenTracingSpan)_tracer
                .BuildSpan("Root")
                .Start();
            var child1 = (OpenTracingSpan)_tracer
                .BuildSpan("Child1")
                .Start();
            child1.Finish();
            var child2 = (OpenTracingSpan)_tracer
                .BuildSpan("Child2")
                .Start();

            Assert.Equal(root.DDSpan.TraceContext, child1.DDSpan.TraceContext);
            Assert.Equal(root.DDSpan.Context.SpanId, child1.DDSpan.Context.ParentId);
            Assert.Equal(root.DDSpan.TraceContext, child2.DDSpan.TraceContext);
            Assert.Equal(root.DDSpan.Context.SpanId, child2.DDSpan.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
        {
            var root = (OpenTracingSpan)_tracer
                .BuildSpan("Root")
                .Start();
            var child1 = (OpenTracingSpan)_tracer
                .BuildSpan("Child1")
                .Start();
            var child2 = (OpenTracingSpan)_tracer
                .BuildSpan("Child2")
                .Start();

            Assert.Equal(root.DDSpan.TraceContext, child1.DDSpan.TraceContext);
            Assert.Equal(root.DDSpan.Context.SpanId, child1.DDSpan.Context.ParentId);
            Assert.Equal(root.DDSpan.TraceContext, child2.DDSpan.TraceContext);
            Assert.Equal(child1.DDSpan.Context.SpanId, child2.DDSpan.Context.ParentId);
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

            Assert.Equal(root.DDSpan.TraceContext, syncChild.DDSpan.TraceContext);
            Assert.Equal(root.DDSpan.Context.SpanId, syncChild.DDSpan.Context.ParentId);
            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(root.DDSpan.TraceContext, span.DDSpan.TraceContext);
                Assert.Equal(root.DDSpan.Context.SpanId, span.DDSpan.Context.ParentId);
            }
        }

        [Fact]
        public void Inject_HttpHeadersFormat_CorrectHeaders()
        {
            var span = (OpenTracingSpan)_tracer.BuildSpan("Span").Start();
            var headers = new MockTextMap();

            _tracer.Inject(span.Context, BuiltinFormats.HttpHeaders, headers);

            Assert.Equal(span.DDSpan.Context.TraceId.ToString(), headers.Get(Constants.HttpHeaderTraceId));
            Assert.Equal(span.DDSpan.Context.SpanId.ToString(), headers.Get(Constants.HttpHeaderParentId));
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
