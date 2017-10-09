using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class TracerTests
    {
        private Mock<IApi> _apiMock = new Mock<IApi>(MockBehavior.Strict);

        [Fact]
        public void BuildSpan_NoParameterAutomaticContextPropagation_DefaultParameters()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: true);

            var builder = tracer.BuildSpan("Op1");
            var span = (Span)builder.Start();

            Assert.Equal(Constants.UnkownService, span.ServiceName);
            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void BuildSpan_NoParameterNoAutomaticContextPropagation_DefaultParameters()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: false);

            var builder = tracer.BuildSpan("Op1");
            var span = (Span)builder.Start();

            Assert.Equal(Constants.UnkownService, span.ServiceName);
            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChildAutomaticContextPropagation_ChildParentProperlySet()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: true);

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();
            var child = (Span)tracer
                .BuildSpan("Child")
                .Start();

            Assert.Equal(root.TraceContext, child.TraceContext);
            Assert.Equal(root.DatadogContext.SpanId, child.DatadogContext.ParentId);
        }

        [Fact]
        public void BuildSpan_2ChildrenOfRootAutomaticContextPropagation_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: true);

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();
            var child1 = (Span)tracer
                .BuildSpan("Child1")
                .Start();
            child1.Finish();
            var child2 = (Span)tracer
                .BuildSpan("Child2")
                .Start();

            Assert.Equal(root.TraceContext, child1.TraceContext);
            Assert.Equal(root.DatadogContext.SpanId, child1.DatadogContext.ParentId);
            Assert.Equal(root.TraceContext, child2.TraceContext);
            Assert.Equal(root.DatadogContext.SpanId, child2.DatadogContext.ParentId);
        }

        [Fact]
        public void BuildSpan_2LevelChildrenAutomaticContextPropagation_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: true);

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();
            var child1 = (Span)tracer
                .BuildSpan("Child1")
                .Start();
            var child2 = (Span)tracer
                .BuildSpan("Child2")
                .Start();

            Assert.Equal(root.TraceContext, child1.TraceContext);
            Assert.Equal(root.DatadogContext.SpanId, child1.DatadogContext.ParentId);
            Assert.Equal(root.TraceContext, child2.TraceContext);
            Assert.Equal(child1.DatadogContext.SpanId, child2.DatadogContext.ParentId);
        }

        [Fact]
        public async Task BuildSpan_AsyncChildrenCreationAutomaticContextPropagation_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_apiMock.Object, automaticContextPropagation: true);
            var tcs = new TaskCompletionSource<bool>();

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();

            Func<Tracer, Task<Span>> createSpanAsync = async (t) => { await tcs.Task; return (Span)tracer.BuildSpan("AsyncChild").Start(); };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(tracer)).ToArray();

            var syncChild = (Span)tracer.BuildSpan("SyncChild").Start();
            tcs.SetResult(true);

            Assert.Equal(root.TraceContext, syncChild.TraceContext);
            Assert.Equal(root.DatadogContext.SpanId, syncChild.DatadogContext.ParentId);
            foreach(var task in tasks)
            {
                var span = await task;
                Assert.Equal(root.TraceContext, span.TraceContext);
                Assert.Equal(root.DatadogContext.SpanId, span.DatadogContext.ParentId);
            }
        }
    }
}
