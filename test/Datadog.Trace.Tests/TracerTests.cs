using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TracerTests
    {
        private readonly Tracer _tracer;

        public TracerTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, null);
        }

        [Fact]
        public void StartActive_SetOperationName_OperationNameIsSet()
        {
            var scope = _tracer.StartActive("Operation", null);

            Assert.Equal("Operation", scope.Span.OperationName);
        }

        [Fact]
        public void StartActive_SetOperationName_ActiveScopeIsSet()
        {
            var scope = _tracer.StartActive("Operation", null);

            var activeScope = _tracer.ActiveScope;
            Assert.Equal(scope, activeScope);
        }

        [Fact]
        public void StartActive_NoActiveScope_RootSpan()
        {
            var scope = _tracer.StartActive("Operation", null);

            Assert.True(scope.Span.IsRootSpan);
        }

        [Fact]
        public void StartActive_ActiveScope_UseCurrentScopeAsParent()
        {
            var parentScope = _tracer.StartActive("Parent");
            var childScope = _tracer.StartActive("Child");

            Assert.Equal(parentScope.Span.Context, childScope.Span.Context.Parent);
        }

        [Fact]
        public void StartActive_IgnoreActiveScope_RootSpan()
        {
            var firstScope = _tracer.StartActive("First");
            var secondScope = _tracer.StartActive("Second", ignoreActiveScope: true);

            Assert.True(secondScope.Span.IsRootSpan);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsClosed()
        {
            var scope = _tracer.StartActive("Operation");
            Assert.False(scope.Span.IsFinished);

            scope.Close();

            Assert.True(scope.Span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsDisposed()
        {
            Scope scope;
            using (scope = _tracer.StartActive("Operation"))
            {
                Assert.False(scope.Span.IsFinished);
            }

            Assert.True(scope.Span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_NoFinishOnClose_SpanIsNotFinishedWhenScopeIsClosed()
        {
            var scope = _tracer.StartActive("Operation", finishOnClose: false);
            Assert.False(scope.Span.IsFinished);

            scope.Dispose();

            Assert.False(scope.Span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_SetParentManually_ParentIsSet()
        {
            var parent = _tracer.StartSpan("Parent");
            var child = _tracer.StartActive("Child", parent.Context);

            Assert.Equal(parent.Context, child.Span.Context.Parent);
        }

        [Fact]
        public void StartActive_SetParentManuallyFromExternalContext_ParentIsSet()
        {
            const ulong traceId = 11;
            const ulong parentId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            var parent = new SpanContext(traceId, parentId, samplingPriority);
            var child = _tracer.StartActive("Child", parent);

            Assert.True(child.Span.IsRootSpan);
            Assert.Equal(traceId, parent.TraceId);
            Assert.Equal(parentId, parent.SpanId);
            Assert.Null(parent.TraceContext);
            Assert.Equal(parent, child.Span.Context.Parent);
            Assert.Equal(parentId, child.Span.Context.ParentId);
            Assert.NotNull(child.Span.Context.TraceContext);
            Assert.Equal(samplingPriority, child.Span.Context.TraceContext.SamplingPriority);
        }

        [Fact]
        public void StartActive_NoServiceName_DefaultServiceName()
        {
            var scope = _tracer.StartActive("Operation");

            Assert.Contains(scope.Span.ServiceName, TestRunners.ValidNames);
        }

        [Fact]
        public void StartActive_SetServiceName_ServiceNameIsSet()
        {
            var scope = _tracer.StartActive("Operation", serviceName: "MyAwesomeService");

            Assert.Equal("MyAwesomeService", scope.Span.ServiceName);
        }

        [Fact]
        public void StartActive_SetParentServiceName_ChildServiceNameIsSet()
        {
            var parent = _tracer.StartActive("Parent", serviceName: "MyAwesomeService");
            var child = _tracer.StartActive("Child");

            Assert.Equal("MyAwesomeService", child.Span.ServiceName);
        }

        [Fact]
        public void StartActive_SetStartTime_StartTimeIsProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var scope = _tracer.StartActive("Operation", startTime: startTime);

            Assert.Equal(startTime, scope.Span.StartTime);
        }

        [Fact]
        public void StartManual_SetOperationName_OperationNameIsSet()
        {
            var span = _tracer.StartSpan("Operation", null);

            Assert.Equal("Operation", span.OperationName);
        }

        [Fact]
        public void StartManual_SetOperationName_ActiveScopeIsNotSet()
        {
            _tracer.StartSpan("Operation", null);

            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartManual_NoActiveScope_RootSpan()
        {
            var scope = _tracer.StartActive("Operation", null);

            Assert.True(scope.Span.IsRootSpan);
        }

        [Fact]
        public void StartManula_ActiveScope_UseCurrentScopeAsParent()
        {
            var parentSpan = _tracer.StartSpan("Parent");
            _tracer.ActivateSpan(parentSpan);
            var childSpan = _tracer.StartSpan("Child");

            Assert.Equal(parentSpan.Context, childSpan.Context.Parent);
        }

        [Fact]
        public void StartManual_IgnoreActiveScope_RootSpan()
        {
            var firstSpan = _tracer.StartSpan("First");
            _tracer.ActivateSpan(firstSpan);
            var secondSpan = _tracer.StartSpan("Second", ignoreActiveScope: true);

            Assert.True(secondSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            var root = _tracer.StartActive("Root");
            var child1 = _tracer.StartActive("Child1");
            child1.Dispose();
            var child2 = _tracer.StartActive("Child2");

            Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)child1.Span.Context.TraceContext);
            Assert.Equal(root.Span.Context.SpanId, child1.Span.Context.ParentId);
            Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)child2.Span.Context.TraceContext);
            Assert.Equal(root.Span.Context.SpanId, child2.Span.Context.ParentId);
        }

        [Fact]
        public void StartActive_2LevelChildren_ChildrenParentProperlySet()
        {
            var root = _tracer.StartActive("Root");
            var child1 = _tracer.StartActive("Child1");
            var child2 = _tracer.StartActive("Child2");

            Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)child1.Span.Context.TraceContext);
            Assert.Equal(root.Span.Context.SpanId, child1.Span.Context.ParentId);
            Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)child2.Span.Context.TraceContext);
            Assert.Equal(child1.Span.Context.SpanId, child2.Span.Context.ParentId);
        }

        [Fact]
        public async Task StartActive_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            var root = _tracer.StartActive("Root");

            Func<Tracer, Task<Scope>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return t.StartActive("AsyncChild");
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = _tracer.StartActive("SyncChild");
            tcs.SetResult(true);

            Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)syncChild.Span.Context.TraceContext);
            Assert.Equal(root.Span.Context.SpanId, syncChild.Span.Context.ParentId);
            foreach (var task in tasks)
            {
                var span = await task;
                Assert.Equal(root.Span.Context.TraceContext, (ITraceContext)span.Span.Context.TraceContext);
                Assert.Equal(root.Span.Context.SpanId, span.Span.Context.ParentId);
            }
        }

        [Theory]
        [InlineData("ddagent", "5000", "http://ddagent:5000")]
        [InlineData("", "", "http://localhost:8126")]
        [InlineData(null, null, "http://localhost:8126")]
        public void SetHostAndPortEnvironmentVariables(string host, string port, string expectedUri)
        {
            string originalHost = Environment.GetEnvironmentVariable("DD_AGENT_HOST");
            string originalPort = Environment.GetEnvironmentVariable("DD_TRACE_AGENT_PORT");

            Environment.SetEnvironmentVariable("DD_AGENT_HOST", host);
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_PORT", port);

            var configuration = new Configuration.TracerSettings(new EnvironmentConfigurationSource());
            Uri uri = Tracer.GetAgentUri(configuration);
            Assert.Equal(new Uri(expectedUri), uri);

            // reset the environment variables to their original values (if any) when done
            Environment.SetEnvironmentVariable("DD_AGENT_HOST", originalHost);
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_PORT", originalPort);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("test")]
        public void SetEnvEnvironmentVariable(string env)
        {
            var name = "DD_ENV";
            string originalEnv = Environment.GetEnvironmentVariable(name);

            Environment.SetEnvironmentVariable(name, env);
            Span span = Tracer.Instance.StartSpan("operation");

            Assert.Equal(env, span.GetTag(Tags.Env));

            // reset the environment variable to its original value (if any) when done
            Environment.SetEnvironmentVariable(name, originalEnv);
        }

        [Theory]
        // if no service name is specified, fallback to a best guess (e.g. assembly name, process name)
        [InlineData(null, null, null, null)]
        // if only one is set, use that one
        [InlineData("envService", null, null, "envService")]
        [InlineData(null, "tracerService", null, "tracerService")]
        [InlineData(null, null, "spanService", "spanService")]
        // if more than one is set, follow precedence: span > tracer > env > default
        [InlineData(null, "tracerService", "spanService", "spanService")]
        [InlineData("envService", null, "spanService", "spanService")]
        [InlineData("envService", "tracerService", null, "tracerService")]
        [InlineData("envService", "tracerService", "spanService", "spanService")]
        public void SetServiceName(string envServiceName, string tracerServiceName, string spanServiceName, string expectedServiceName)
        {
            var name = "DD_SERVICE_NAME";
            string originalEnv = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, envServiceName);

            var tracer = Tracer.Create(defaultServiceName: tracerServiceName);
            Span span = tracer.StartSpan("operationName", serviceName: spanServiceName);

            if (expectedServiceName == null)
            {
                Assert.Contains(span.ServiceName, TestRunners.ValidNames);
            }
            else
            {
                Assert.Equal(expectedServiceName, span.ServiceName);
            }

            // reset the environment variable to its original values (if any) when done
            Environment.SetEnvironmentVariable(name, originalEnv);
        }
    }
}
