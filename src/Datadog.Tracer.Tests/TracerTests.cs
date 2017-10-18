using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Datadog.Tracer.Tests
{
    public class TracerTests
    {
        private Mock<IAgentWriter> _agentWriter = new Mock<IAgentWriter>();

        [Fact]
        public void Ctor_DefaultValues_ShouldSendDefaultServiceInfo()
        {
            var tracer = new Tracer(_agentWriter.Object);
            _agentWriter.Verify(x => x.WriteServiceInfo(It.Is<ServiceInfo>(y => y.ServiceName == "Datadog.Tracer" && y.AppType == Constants.WebAppType && y.App == Constants.UnkownApp)), Times.Once);
        }

        [Fact]
        public void BuildSpan_NoParameter_DefaultParameters()
        {
            var tracer = new Tracer(_agentWriter.Object);

            var builder = tracer.BuildSpan("Op1");
            var span = (Span)builder.Start();

            Assert.Equal("Datadog.Tracer", span.ServiceName);
            Assert.Equal("Op1", span.OperationName);
        }

        [Fact]
        public void BuildSpan_OneChild_ChildParentProperlySet()
        {
            var tracer = new Tracer(_agentWriter.Object);

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();
            var child = (Span)tracer
                .BuildSpan("Child")
                .Start();

            Assert.Equal(root.TraceContext, child.TraceContext);
            Assert.Equal(root.Context.SpanId, child.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_agentWriter.Object);

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
            Assert.Equal(root.Context.SpanId, child1.Context.ParentId);
            Assert.Equal(root.TraceContext, child2.TraceContext);
            Assert.Equal(root.Context.SpanId, child2.Context.ParentId);
        }

        [Fact]
        public void BuildSpan_2LevelChildren_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_agentWriter.Object);

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
            Assert.Equal(root.Context.SpanId, child1.Context.ParentId);
            Assert.Equal(root.TraceContext, child2.TraceContext);
            Assert.Equal(child1.Context.SpanId, child2.Context.ParentId);
        }

        [Fact]
        public async Task BuildSpan_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tracer = new Tracer(_agentWriter.Object);
            var tcs = new TaskCompletionSource<bool>();

            var root = (Span)tracer
                .BuildSpan("Root")
                .Start();

            Func<Tracer, Task<Span>> createSpanAsync = async (t) => { await tcs.Task; return (Span)tracer.BuildSpan("AsyncChild").Start(); };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(tracer)).ToArray();

            var syncChild = (Span)tracer.BuildSpan("SyncChild").Start();
            tcs.SetResult(true);

            Assert.Equal(root.TraceContext, syncChild.TraceContext);
            Assert.Equal(root.Context.SpanId, syncChild.Context.ParentId);
            foreach(var task in tasks)
            {
                var span = await task;
                Assert.Equal(root.TraceContext, span.TraceContext);
                Assert.Equal(root.Context.SpanId, span.Context.ParentId);
            }
        }
    }
}
