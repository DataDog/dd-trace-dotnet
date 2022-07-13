// <copyright file="TracerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Headers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Tests.PlatformHelpers;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests
{
    [Collection(nameof(WebRequestCollection))]
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
            var scope = _tracer.StartActive("Operation");

            Assert.Equal("Operation", scope.Span.OperationName);
        }

        [Fact]
        public void StartActive_SetOperationName_ActiveScopeIsSet()
        {
            var scope = _tracer.StartActive("Operation");

            var activeScope = _tracer.ActiveScope;
            Assert.Equal(scope, activeScope);
        }

        [Fact]
        public void StartActive_NoActiveScope_RootSpan()
        {
            var scope = (Scope)_tracer.StartActive("Operation");
            var span = scope.Span;

            Assert.True(span.IsRootSpan);
        }

        [Fact]
        public void StartActive_ActiveScope_UseCurrentScopeAsParent()
        {
            var parentScope = (Scope)_tracer.StartActive("Parent");
            var childScope = (Scope)_tracer.StartActive("Child");

            var parentSpan = parentScope.Span;
            var childSpan = childScope.Span;

            Assert.Equal(parentSpan.Context, childSpan.Context.Parent);
        }

        [Fact]
        public void StartActive_IgnoreActiveScope_RootSpan()
        {
            var firstScope = _tracer.StartActive("First");
            var secondScope = (Scope)_tracer.StartActive("Second", new SpanCreationSettings { Parent = SpanContext.None });
            var secondSpan = secondScope.Span;

            Assert.True(secondSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsClosed()
        {
            var scope = (Scope)_tracer.StartActive("Operation");
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.Close();

            Assert.True(span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_FinishOnClose_SpanIsFinishedWhenScopeIsDisposed()
        {
            Scope scope;
            Span span;
            using (scope = (Scope)_tracer.StartActive("Operation"))
            {
                span = scope.Span;
                Assert.False(span.IsFinished);
            }

            Assert.True(span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartActive_FinishOnClose_SpanIsFinishedCorrectlyWhenSetFinishOnCloseAndScopeIsClosed(bool newFinishOnClose)
        {
            var scope = (Scope)_tracer.StartActive("Operation");
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.SetFinishOnClose(newFinishOnClose);
            scope.Close();

            Assert.Equal(newFinishOnClose, span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartActive_FinishOnClose_SpanIsFinishedCorrectlyWhenSetFinishOnCloseAndScopeIsDisposed(bool newFinishOnClose)
        {
            Scope scope;
            Span span;
            using (scope = (Scope)_tracer.StartActive("Operation"))
            {
                span = scope.Span;
                scope.SetFinishOnClose(newFinishOnClose);
                Assert.False(span.IsFinished);
            }

            Assert.Equal(newFinishOnClose, span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_NoFinishOnClose_SpanIsNotFinishedWhenScopeIsClosed()
        {
            var spanCreationSettings = new SpanCreationSettings() { FinishOnClose = false };
            var scope = (Scope)_tracer.StartActive("Operation", spanCreationSettings);
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.Close();

            Assert.False(span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_NoFinishOnClose_SpanIsNotFinishedWhenScopeIsDisposed()
        {
            var spanCreationSettings = new SpanCreationSettings() { FinishOnClose = false };
            var scope = (Scope)_tracer.StartActive("Operation", spanCreationSettings);
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.Dispose();

            Assert.False(span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartActive_NoFinishOnClose_SpanIsFinishedCorrectlyWhenSetFinishOnCloseAndScopeIsClosed(bool newFinishOnClose)
        {
            var spanCreationSettings = new SpanCreationSettings() { FinishOnClose = false };
            var scope = (Scope)_tracer.StartActive("Operation", spanCreationSettings);
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.SetFinishOnClose(newFinishOnClose);
            scope.Close();

            Assert.Equal(newFinishOnClose, span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StartActive_NoFinishOnClose_SpanIsFinishedCorrectlyWhenSetFinishOnCloseAndScopeIsDisposed(bool newFinishOnClose)
        {
            var spanCreationSettings = new SpanCreationSettings() { FinishOnClose = false };
            var scope = (Scope)_tracer.StartActive("Operation", spanCreationSettings);
            var span = scope.Span;
            Assert.False(span.IsFinished);

            scope.SetFinishOnClose(newFinishOnClose);
            scope.Dispose();

            Assert.Equal(newFinishOnClose, span.IsFinished);
            Assert.Null(_tracer.ActiveScope);
        }

        [Fact]
        public void StartActive_SetParentManually_ParentIsSet()
        {
            var parent = _tracer.StartSpan("Parent");

            var spanCreationSettings = new SpanCreationSettings() { Parent = parent.Context };
            var childScope = (Scope)_tracer.StartActive("Child", spanCreationSettings);
            var childSpan = childScope.Span;

            Assert.Equal(parent.Context, childSpan.Context.Parent);
        }

        [Fact]
        public void StartActive_SetParentManuallyFromExternalContext_ParentIsSet()
        {
            const ulong traceId = 11;
            const ulong parentId = 7;
            const int samplingPriority = SamplingPriorityValues.UserKeep;

            var parent = new SpanContext(traceId, parentId, (SamplingPriority)samplingPriority);
            var spanCreationSettings = new SpanCreationSettings() { Parent = parent };
            var child = (Scope)_tracer.StartActive("Child", spanCreationSettings);
            var childSpan = child.Span;

            Assert.True(childSpan.IsRootSpan);
            Assert.Equal(traceId, parent.TraceId);
            Assert.Equal(parentId, parent.SpanId);
            Assert.Null(parent.TraceContext);
            Assert.Equal(parent, childSpan.Context.Parent);
            Assert.Equal(parentId, childSpan.Context.ParentId);
            Assert.NotNull(childSpan.Context.TraceContext);
            Assert.Equal(samplingPriority, childSpan.Context.TraceContext.SamplingPriority);
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
            var scope = _tracer.StartActive("Operation");
            scope.Span.ServiceName = "MyAwesomeService";

            Assert.Equal("MyAwesomeService", scope.Span.ServiceName);
        }

        [Fact]
        public void StartActive_SetParentServiceName_ChildServiceNameIsDefaultServiceName()
        {
            var parent = _tracer.StartActive("Parent");
            parent.Span.ServiceName = "MyAwesomeService";
            var child = _tracer.StartActive("Child");

            Assert.NotEqual("MyAwesomeService", child.Span.ServiceName);
            Assert.Equal(_tracer.DefaultServiceName, child.Span.ServiceName);
        }

        [Fact]
        public void StartActive_SetStartTime_StartTimeIsProperlySet()
        {
            var startTime = new DateTimeOffset(2017, 01, 01, 0, 0, 0, TimeSpan.Zero);
            var spanCreationSettings = new SpanCreationSettings() { StartTime = startTime };
            var scope = _tracer.StartActive("Operation", spanCreationSettings);
            var span = (Span)scope.Span;

            Assert.Equal(startTime, span.StartTime);
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
            var span = (Span)scope.Span;

            Assert.True(span.IsRootSpan);
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
            var secondSpan = _tracer.StartSpan("Second", parent: SpanContext.None);

            Assert.True(secondSpan.IsRootSpan);
        }

        [Fact]
        public void StartActive_2ChildrenOfRoot_ChildrenParentProperlySet()
        {
            var root = (Scope)_tracer.StartActive("Root");
            var rootSpan = root.Span;

            var child1 = (Scope)_tracer.StartActive("Child1");
            var child1Span = child1.Span;
            child1.Dispose();

            var child2 = (Scope)_tracer.StartActive("Child2");
            var child2Span = child2.Span;

            Assert.Equal(rootSpan.Context.TraceContext, child1Span.Context.TraceContext);
            Assert.Equal(rootSpan.Context.SpanId, child1Span.Context.ParentId);
            Assert.Equal(rootSpan.Context.TraceContext, child2Span.Context.TraceContext);
            Assert.Equal(rootSpan.Context.SpanId, child2Span.Context.ParentId);
        }

        [Fact]
        public void StartActive_2LevelChildren_ChildrenParentProperlySet()
        {
            var root = (Scope)_tracer.StartActive("Root");
            var child1 = (Scope)_tracer.StartActive("Child1");
            var child2 = (Scope)_tracer.StartActive("Child2");

            var rootSpan = root.Span;
            var child1Span = child1.Span;
            var child2Span = child2.Span;

            Assert.Equal(rootSpan.Context.TraceContext, child1Span.Context.TraceContext);
            Assert.Equal(rootSpan.Context.SpanId, child1Span.Context.ParentId);
            Assert.Equal(rootSpan.Context.TraceContext, child2Span.Context.TraceContext);
            Assert.Equal(child1Span.Context.SpanId, child2Span.Context.ParentId);
        }

        [Fact]
        public async Task StartActive_AsyncChildrenCreation_ChildrenParentProperlySet()
        {
            var tcs = new TaskCompletionSource<bool>();

            var root = (Scope)_tracer.StartActive("Root");
            var rootSpan = root.Span;

            Func<Tracer, Task<IScope>> createSpanAsync = async (t) =>
            {
                await tcs.Task;
                return t.StartActive("AsyncChild");
            };
            var tasks = Enumerable.Range(0, 10).Select(x => createSpanAsync(_tracer)).ToArray();

            var syncChild = (Scope)_tracer.StartActive("SyncChild");
            var syncChildSpan = syncChild.Span;
            tcs.SetResult(true);

            Assert.Equal(rootSpan.Context.TraceContext, syncChildSpan.Context.TraceContext);
            Assert.Equal(rootSpan.Context.SpanId, syncChildSpan.Context.ParentId);
            foreach (var task in tasks)
            {
                var scope = (Scope)await task;
                var span = scope.Span;

                Assert.Equal(rootSpan.Context.TraceContext, span.Context.TraceContext);
                Assert.Equal(rootSpan.Context.SpanId, span.Context.ParentId);
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
                // due to the service name fallback, if this runs at the same time as AzureAppServicesMetadataTests,
                // then AzureAppServices.IsRelevant returns true, and we may pull the service name from the AAS env vars
                var expectedServiceNames = TestRunners.ValidNames.Concat(new[] { AzureAppServicesTests.DeploymentId });
                Assert.Contains(span.ServiceName, expectedServiceNames);
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
            const int samplingPriority = SamplingPriorityValues.UserKeep;
            const string origin = "synthetics";

            var propagatedContext = new SpanContext(traceId, spanId, samplingPriority, null, origin);
            Assert.Equal(origin, propagatedContext.Origin);

            var spanCreationSettings = new SpanCreationSettings() { Parent = propagatedContext };
            using var firstScope = (Scope)_tracer.StartActive("First Span", spanCreationSettings);
            var firstSpan = (Span)firstScope.Span;

            Assert.True(firstSpan.IsRootSpan);
            Assert.Equal(origin, firstSpan.Context.Origin);
            Assert.Equal(origin, firstSpan.GetTag(Tags.Origin));

            var spanCreationSettings2 = new SpanCreationSettings() { Parent = firstSpan.Context };
            using var secondScope = (Scope)_tracer.StartActive("Child", spanCreationSettings2);
            var secondSpan = (Span)secondScope.Span;

            Assert.False(secondSpan.IsRootSpan);
            Assert.Equal(origin, secondSpan.Context.Origin);
            Assert.Equal(origin, secondSpan.GetTag(Tags.Origin));
        }

        [Fact]
        public void OriginHeader_InjectFromChildSpan()
        {
            const ulong traceId = 9;
            const ulong spanId = 7;
            const int samplingPriority = SamplingPriorityValues.UserKeep;
            const string origin = "synthetics";

            var propagatedContext = new SpanContext(traceId, spanId, samplingPriority, null, origin);

            var spanCreationSettings = new SpanCreationSettings() { Parent = propagatedContext };
            using var firstScope = (Scope)_tracer.StartActive("First Span", spanCreationSettings);
            var firstSpan = firstScope.Span;

            var spanCreationSettings2 = new SpanCreationSettings() { Parent = firstSpan.Context };
            using var secondScope = (Scope)_tracer.StartActive("Child", spanCreationSettings2);
            var secondSpan = secondScope.Span;

            IHeadersCollection headers = WebRequest.CreateHttp("http://localhost").Headers.Wrap();

            SpanContextPropagator.Instance.Inject(secondSpan.Context, headers);
            var resultContext = SpanContextPropagator.Instance.Extract(headers);

            Assert.NotNull(resultContext);
            Assert.Equal(firstSpan.Context.Origin, resultContext.Origin);
            Assert.Equal(secondSpan.Context.Origin, resultContext.Origin);
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

        [Fact]
        public void SetUserOnRootSpanDirectly_ShouldSetOnTrace()
        {
            var scopeManager = new AsyncLocalScopeManager();

            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };
            var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ISampler>(), scopeManager, Mock.Of<IDogStatsd>());

            var rootTestScope = (Scope)tracer.StartActive("test.trace");
            var childTestScope = (Scope)tracer.StartActive("test.trace.child");
            childTestScope.Dispose();

            var email = "test@adventure-works.com";
            var name = "Jane Doh";
            var id = Guid.NewGuid().ToString();
            var sessionId = Guid.NewGuid().ToString();
            var role = "admin";
            var scope = "read:message, write:files";

            var userDetails = new UserDetails()
            {
                Email = email,
                Name = name,
                Id = id,
                SessionId = sessionId,
                Role = role,
                Scope = scope,
            };
            tracer.ActiveScope?.Span.SetUser(userDetails);

            var traceContext = rootTestScope.Span.Context.TraceContext;
            Assert.Equal(email, traceContext.Tags.GetTag(Tags.User.Email));
            Assert.Equal(name, traceContext.Tags.GetTag(Tags.User.Name));
            Assert.Equal(id, traceContext.Tags.GetTag(Tags.User.Id));
            Assert.Equal(sessionId, traceContext.Tags.GetTag(Tags.User.SessionId));
            Assert.Equal(role, traceContext.Tags.GetTag(Tags.User.Role));
            Assert.Equal(scope, traceContext.Tags.GetTag(Tags.User.Scope));
        }

        [Fact]
        public void SetUserOnChildChildSpan_ShouldSetOnTrace()
        {
            var scopeManager = new AsyncLocalScopeManager();

            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };
            var tracer = new Tracer(settings, Mock.Of<IAgentWriter>(), Mock.Of<ISampler>(), scopeManager, Mock.Of<IDogStatsd>());

            var rootTestScope = (Scope)tracer.StartActive("test.trace");
            var childTestScope = (Scope)tracer.StartActive("test.trace.child");

            var email = "test@adventure-works.com";
            var name = "Jane Doh";
            var id = Guid.NewGuid().ToString();
            var sessionId = Guid.NewGuid().ToString();
            var role = "admin";
            var scope = "read:message, write:files";

            var userDetails = new UserDetails()
            {
                Email = email,
                Name = name,
                Id = id,
                SessionId = sessionId,
                Role = role,
                Scope = scope,
            };
            tracer.ActiveScope?.Span.SetUser(userDetails);

            childTestScope.Dispose();

            var traceContext = rootTestScope.Span.Context.TraceContext;
            Assert.Equal(email, traceContext.Tags.GetTag(Tags.User.Email));
            Assert.Equal(name, traceContext.Tags.GetTag(Tags.User.Name));
            Assert.Equal(id, traceContext.Tags.GetTag(Tags.User.Id));
            Assert.Equal(sessionId, traceContext.Tags.GetTag(Tags.User.SessionId));
            Assert.Equal(role, traceContext.Tags.GetTag(Tags.User.Role));
            Assert.Equal(scope, traceContext.Tags.GetTag(Tags.User.Scope));
        }

        [Fact]
        public void SetUser_ShouldWorkOnAnythingImplementingISpan()
        {
            var testSpan = new SpanStub();

            var email = "test@adventure-works.com";
            var name = "Jane Doh";
            var id = Guid.NewGuid().ToString();
            var sessionId = Guid.NewGuid().ToString();
            var role = "admin";
            var scope = "read:message, write:files";

            var userDetails = new UserDetails()
            {
                Email = email,
                Name = name,
                Id = id,
                SessionId = sessionId,
                Role = role,
                Scope = scope,
            };
            testSpan.SetUser(userDetails);

            Assert.Equal(email, testSpan.GetTag(Tags.User.Email));
            Assert.Equal(name, testSpan.GetTag(Tags.User.Name));
            Assert.Equal(id, testSpan.GetTag(Tags.User.Id));
            Assert.Equal(sessionId, testSpan.GetTag(Tags.User.SessionId));
            Assert.Equal(role, testSpan.GetTag(Tags.User.Role));
            Assert.Equal(scope, testSpan.GetTag(Tags.User.Scope));
        }

        [Fact]
        public void SetUser_ShouldThrowAnExceptionIfNoIdIsProvided()
        {
            var testSpan = new SpanStub();

            var email = "test@adventure-works.com";

            var userDetails = new UserDetails()
            {
                Email = email,
            };

            Assert.ThrowsAny<ArgumentException>(() =>
                testSpan.SetUser(userDetails));
        }

        private class SpanStub : ISpan
        {
            private Dictionary<string, string> _tags = new Dictionary<string, string>();

            public string OperationName { get; set; }

            public string ResourceName { get; set; }

            public string Type { get; set; }

            public bool Error { get; set; }

            public string ServiceName { get; set; }

            public ulong TraceId => 1ul;

            public ulong SpanId => 1ul;

            public ISpanContext Context => null;

            public void Dispose()
            {
            }

            public void Finish()
            {
            }

            public void Finish(DateTimeOffset finishTimestamp)
            {
            }

            public string GetTag(string key)
            {
                _tags.TryGetValue(key, out var value);
                return value;
            }

            public void SetException(Exception exception)
            {
            }

            public ISpan SetTag(string key, string value)
            {
                _tags[key] = value;
                return this;
            }
        }
    }
}
