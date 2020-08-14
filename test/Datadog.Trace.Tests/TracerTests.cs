using System;
using System.Linq;
using System.Net;
#if NET452
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Services;
#endif
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    public class TracerTests
    {
        private readonly Tracer _tracer;

        static TracerTests()
        {
#if NET452
            LifetimeServices.LeaseTime = TimeSpan.FromMilliseconds(100);
            LifetimeServices.LeaseManagerPollTime = TimeSpan.FromMilliseconds(10);
#endif
        }

        public TracerTests()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            _tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
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
        [InlineData(null)]
        [InlineData("test")]
        public void SetEnv(string env)
        {
            var settings = new TracerSettings()
            {
                Environment = env,
            };

            var tracer = new Tracer(settings);
            Span span = tracer.StartSpan("operation");

            Assert.Equal(env, span.GetTag(Tags.Env));
        }

        [Theory]
        // if no service name is specified, fallback to a best guess (e.g. assembly name, process name)
        [InlineData(null, null, null)]
        // if only one is set, use that one
        [InlineData("tracerService", null, "tracerService")]
        [InlineData(null, "spanService", "spanService")]
        // if more than one is set, follow precedence: span > tracer  > default
        [InlineData("tracerService", "spanService", "spanService")]
        public void SetServiceName(string tracerServiceName, string spanServiceName, string expectedServiceName)
        {
            var settings = new TracerSettings()
            {
                ServiceName = tracerServiceName,
            };

            var tracer = new Tracer(settings);
            Span span = tracer.StartSpan("operationName", serviceName: spanServiceName);

            if (expectedServiceName == null)
            {
                Assert.Contains(span.ServiceName, TestRunners.ValidNames);
            }
            else
            {
                Assert.Equal(expectedServiceName, span.ServiceName);
            }
        }

        [Fact]
        public void OriginHeader_RootSpanTag()
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var propagatedContext = new SpanContext(traceId, spanId, samplingPriority, null, origin);
            Assert.Equal(origin, propagatedContext.Origin);

            using var firstSpan = _tracer.StartActive("First Span", propagatedContext);
            Assert.True(firstSpan.Span.IsRootSpan);
            Assert.Equal(origin, firstSpan.Span.Context.Origin);
            Assert.Equal(origin, firstSpan.Span.GetTag(Tags.Origin));

            using var secondSpan = _tracer.StartActive("Child", firstSpan.Span.Context);
            Assert.False(secondSpan.Span.IsRootSpan);
            Assert.Equal(origin, secondSpan.Span.Context.Origin);
            Assert.Null(secondSpan.Span.GetTag(Tags.Origin));
        }

        [Fact]
        public void OriginHeader_InjectFromChildSpan()
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var propagatedContext = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            using var firstSpan = _tracer.StartActive("First Span", propagatedContext);
            using var secondSpan = _tracer.StartActive("Child", firstSpan.Span.Context);

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();

            SpanContextPropagator.Instance.Inject(secondSpan.Span.Context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(firstSpan.Span.Context.Origin, resultContext.Origin);
            Assert.Equal(secondSpan.Span.Context.Origin, resultContext.Origin);
            Assert.Equal(origin, resultContext.Origin);
        }
#if NET452

        // Test that storage in the Logical Call Context does not expire
        // See GitHub issue https://github.com/serilog/serilog/issues/987
        // and the associated PR https://github.com/serilog/serilog/pull/992
        [Fact]
        public void DoesNotThrowOnCrossDomainCallsWhenLeaseExpired()
        {
            // Arrange
            var remote = AppDomain.CreateDomain("Remote", null, AppDomain.CurrentDomain.SetupInformation);
            string operationName = "test-span";

            // Act
            try
            {
                using (_tracer.StartActive(operationName))
                {
                    remote.DoCallBack(SleepForLeaseManagerPollCallback);

                    // After the lease expires, access the active scope
                    Scope scope = _tracer.ActiveScope;
                    Assert.Equal(operationName, scope.Span.OperationName);
                }
            }
            finally
            {
                AppDomain.Unload(remote);
            }

            // Assert
            // Nothing. We should just throw no exceptions here
        }

        [Fact]
        public void DisconnectRemoteObjectsAfterCrossDomainCallsOnDispose()
        {
            // Arrange
            var cde = new CountdownEvent(2);
            var tracker = new InMemoryRemoteObjectTracker(cde);
            TrackingServices.RegisterTrackingHandler(tracker);

            var remote = AppDomain.CreateDomain("Remote", null, AppDomain.CurrentDomain.SetupInformation);

            // Act
            try
            {
                using (_tracer.StartActive("test-span"))
                {
                    remote.DoCallBack(EmptyCallback);

                    using (_tracer.StartActive("test-span-inner"))
                    {
                        remote.DoCallBack(EmptyCallback);
                    }
                }
            }
            finally
            {
                AppDomain.Unload(remote);
            }

            // Ensure that we wait long enough for the lease manager poll to occur.
            // Even though we reset LifetimeServices.LeaseManagerPollTime to a shorter duration,
            // the default value is 10 seconds so the first poll may not be affected by our modification
            bool eventSet = cde.Wait(TimeSpan.FromSeconds(30));

            // Assert
            Assert.True(eventSet);
            Assert.Equal(2, tracker.DisconnectCount);
        }

        // Ensure the remote call takes long enough for the lease manager poll to occur.
        // Even though we reset LifetimeServices.LeaseManagerPollTime to a shorter duration,
        // the default value is 10 seconds so the first poll may not be affected by our modification
        private static void SleepForLeaseManagerPollCallback() => Thread.Sleep(TimeSpan.FromSeconds(12));

        private static void EmptyCallback()
        {
        }

        private class InMemoryRemoteObjectTracker : ITrackingHandler
        {
            private CountdownEvent _cde;

            public InMemoryRemoteObjectTracker(CountdownEvent cde)
            {
                _cde = cde;
            }

            public int DisconnectCount { get; set; }

            public void DisconnectedObject(object obj)
            {
                DisconnectCount++;
                _cde.Signal();
            }

            public void MarshaledObject(object obj, ObjRef or)
            {
            }

            public void UnmarshaledObject(object obj, ObjRef or)
            {
            }
        }
#endif
    }
}
