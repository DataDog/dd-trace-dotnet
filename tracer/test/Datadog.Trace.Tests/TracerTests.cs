// <copyright file="TracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
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
            var scope = (Scope)_tracer.StartActive("Operation", null);

            Assert.True(scope.InternalSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_ActiveScope_UseCurrentScopeAsParent()
        {
            var parentScope = (Scope)_tracer.StartActive("Parent");
            var childScope = (Scope)_tracer.StartActive("Child");

            Assert.Equal(parentScope.InternalSpan.Context, childScope.InternalSpan.Context.Parent);
        }

        [Fact]
        public void StartActive_IgnoreActiveScope_RootSpan()
        {
            var firstScope = _tracer.StartActive("First");
            var secondScope = (Scope)_tracer.StartActive("Second", ignoreActiveScope: true);

            Assert.True(secondScope.InternalSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsClosed()
        {
            var scope = (Scope)_tracer.StartActive("Operation");
            Assert.False(scope.InternalSpan.IsFinished);

            scope.Close();

            Assert.True(scope.InternalSpan.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsDisposed()
        {
            Scope scope;
            using (scope = (Scope)_tracer.StartActive("Operation"))
            {
                Assert.False(scope.InternalSpan.IsFinished);
            }

            Assert.True(scope.InternalSpan.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_NoFinishOnClose_SpanIsNotFinishedWhenScopeIsClosed()
        {
            var scope = (Scope)_tracer.StartActive("Operation", finishOnClose: false);
            Assert.False(scope.InternalSpan.IsFinished);

            scope.Dispose();

            Assert.False(scope.InternalSpan.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_SetParentManually_ParentIsSet()
        {
            var parent = (Span)_tracer.StartSpan("Parent");
            var child = (Scope)_tracer.StartActive("Child", parent.Context);

            Assert.Equal(parent.Context, child.InternalSpan.Context.Parent);
        }

        [Fact]
        public void StartActive_SetParentManuallyFromExternalContext_ParentIsSet()
        {
            const ulong traceId = 11;
            const ulong parentId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;

            var parent = new SpanContext(traceId, parentId, samplingPriority);
            var child = (Scope)_tracer.StartActive("Child", parent);

            Assert.True(child.InternalSpan.IsRootSpan);
            Assert.Equal(traceId, parent.TraceId);
            Assert.Equal(parentId, parent.SpanId);
            Assert.Null(parent.TraceContext);
            Assert.Equal(parent, child.InternalSpan.Context.Parent);
            Assert.Equal(parentId, child.InternalSpan.Context.ParentId);
            Assert.NotNull(child.InternalSpan.Context.TraceContext);
            Assert.Equal(samplingPriority, child.InternalSpan.Context.TraceContext.SamplingPriority);
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

            Assert.Equal(startTime, ((Scope)scope).InternalSpan.StartTime);
        }

        [Fact]
        public void StartManual_SetOperationName_OperationNameIsSet()
        {
            var span = _tracer.StartSpan("Operation");

            Assert.Equal("Operation", span.OperationName);
        }

        [Fact]
        public void StartManual_SetOperationName_ActiveScopeIsNotSet()
        {
            _tracer.StartSpan("Operation");

            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartManual_NoActiveScope_RootSpan()
        {
            var scope = _tracer.StartActive("Operation");

            Assert.True(((Scope)scope).InternalSpan.IsRootSpan);
        }

        [Fact]
        public void StartManula_ActiveScope_UseCurrentScopeAsParent()
        {
            var parentSpan = _tracer.StartSpan("Parent");
            _tracer.ActivateSpan(parentSpan);
            var childSpan = _tracer.StartSpan("Child");

            Assert.Equal(((Span)parentSpan).Context, ((Span)childSpan).Context.Parent);
        }

        [Fact]
        public void StartManual_IgnoreActiveScope_RootSpan()
        {
            var firstSpan = _tracer.StartSpan("First");
            _tracer.ActivateSpan(firstSpan);
            var secondSpan = (Span)_tracer.StartSpan("Second", ignoreActiveScope: true);

            Assert.True(secondSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            var root = (Scope)_tracer.StartActive("Root");
            var child1 = (Scope)_tracer.StartActive("Child1");
            child1.Dispose();
            var child2 = (Scope)_tracer.StartActive("Child2");

            Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)child1.InternalSpan.Context.TraceContext);
            Assert.Equal(root.InternalSpan.Context.SpanId, child1.InternalSpan.Context.ParentId);
            Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)child2.InternalSpan.Context.TraceContext);
            Assert.Equal(root.InternalSpan.Context.SpanId, child2.InternalSpan.Context.ParentId);
        }

        [Fact]
        public void StartActive_2LevelChildren_ChildrenParentProperlySet()
        {
            var root = (Scope)_tracer.StartActive("Root");
            var child1 = (Scope)_tracer.StartActive("Child1");
            var child2 = (Scope)_tracer.StartActive("Child2");

            Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)child1.InternalSpan.Context.TraceContext);
            Assert.Equal(root.InternalSpan.Context.SpanId, child1.InternalSpan.Context.ParentId);
            Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)child2.InternalSpan.Context.TraceContext);
            Assert.Equal(child1.InternalSpan.Context.SpanId, child2.InternalSpan.Context.ParentId);
        }

        [Fact]
        public async Task StartActive_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            var root = (Scope)_tracer.StartActive("Root");

            Func<Tracer, Task<IScope>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return t.StartActive("AsyncChild");
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = (Scope)_tracer.StartActive("SyncChild");
            tcs.SetResult(true);

            Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)syncChild.InternalSpan.Context.TraceContext);
            Assert.Equal(root.InternalSpan.Context.SpanId, syncChild.InternalSpan.Context.ParentId);
            foreach (var task in tasks)
            {
                var span = (Scope)await task;
                Assert.Equal(root.InternalSpan.Context.TraceContext, (ITraceContext)span.InternalSpan.Context.TraceContext);
                Assert.Equal(root.InternalSpan.Context.SpanId, span.InternalSpan.Context.ParentId);
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

            var tracer = TracerHelper.Create(settings);
            ISpan span = tracer.StartSpan("operation");

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

            var tracer = TracerHelper.Create(settings);
            ISpan span = tracer.StartSpan("operationName", serviceName: spanServiceName);

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

            using var firstSpan = (Scope)_tracer.StartActive("First Span", propagatedContext);
            Assert.True(firstSpan.InternalSpan.IsRootSpan);
            Assert.Equal(origin, firstSpan.InternalSpan.Context.Origin);
            Assert.Equal(origin, firstSpan.Span.GetTag(Tags.Origin));

            using var secondSpan = (Scope)_tracer.StartActive("Child", firstSpan.InternalSpan.Context);
            Assert.False(secondSpan.InternalSpan.IsRootSpan);
            Assert.Equal(origin, secondSpan.InternalSpan.Context.Origin);
            Assert.Equal(origin, secondSpan.Span.GetTag(Tags.Origin));
        }

        [Fact]
        public void OriginHeader_InjectFromChildSpan()
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const SamplingPriority samplingPriority = SamplingPriority.UserKeep;
            const string origin = "synthetics";

            var propagatedContext = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            using var firstSpan = (Scope)_tracer.StartActive("First Span", propagatedContext);
            using var secondSpan = (Scope)_tracer.StartActive("Child", firstSpan.InternalSpan.Context);

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();

            SpanContextPropagator.Instance.Inject(secondSpan.InternalSpan.Context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(firstSpan.InternalSpan.Context.Origin, resultContext.Origin);
            Assert.Equal(secondSpan.InternalSpan.Context.Origin, resultContext.Origin);
            Assert.Equal(origin, resultContext.Origin);
        }

        [Fact]
        public void RuntimeId()
        {
            var runtimeId = Tracer.RuntimeId;

            // Runtime id should be stable for a given process
            Tracer.RuntimeId.Should().Be(runtimeId);

            // Runtime id should be a UUID
            Guid.TryParse(runtimeId, out _).Should().BeTrue();
        }

        [Fact]
        public async Task ForceFlush()
        {
            var agent = new Mock<IAgentWriter>();

            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            var tracer = new Tracer(settings, agent.Object, Mock.Of<ISampler>(), Mock.Of<IScopeManager>(), Mock.Of<IDogStatsd>());

            await tracer.ForceFlushAsync();

            agent.Verify(a => a.FlushTracesAsync(), Times.Once);
        }
    }
}
