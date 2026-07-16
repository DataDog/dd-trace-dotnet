// <copyright file="LegacyAspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class LegacyAspNetCoreDiagnosticObserverTests
    {
        private const ulong IncomingTraceId = 123456789;
        private const ulong IncomingParentId = 987654321;
        private const string StartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
        private const string StopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
        private const string HostingUnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
        private const string DiagnosticsUnhandledExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
        private const string MvcBeforeActionEvent = "Microsoft.AspNetCore.Mvc.BeforeAction";

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public async Task ActivationRequiresFrameworkFeatureAndAspNetCoreIntegration(
            bool frameworkFeatureEnabled,
            bool aspNetCoreIntegrationEnabled,
            bool expected)
        {
            var aspNetCoreEnabledKey = IntegrationNameToKeys.GetIntegrationEnabledKeys(nameof(IntegrationId.AspNetCore)).Key;
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.FeatureFlags.AspNetCoreNetFrameworkEnabled, frameworkFeatureEnabled.ToString() },
                        { aspNetCoreEnabledKey, aspNetCoreIntegrationEnabled.ToString() },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Microsoft.AspNetCore");
            listener.Setup(
                        instance => instance.Subscribe(
                            It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                            It.IsAny<Predicate<string>>()))
                    .Returns(Mock.Of<IDisposable>());
            var observer = new LegacyAspNetCoreDiagnosticObserver(
                () => tracer,
                Mock.Of<IDatadogLogger>(),
                Mock.Of<IMetricsTelemetryCollector>());

            using var subscription = observer.SubscribeIfMatch(listener.Object);

            (subscription is not null).Should().Be(expected);
            listener.Verify(
                instance => instance.Subscribe(
                    It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                    It.IsAny<Predicate<string>>()),
                expected ? Times.Once() : Times.Never());
        }

        [Fact]
        public void NonMatchingListenerDoesNotResolveTracer()
        {
            var factoryCalls = 0;
            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Other.Listener");
            var observer = new LegacyAspNetCoreDiagnosticObserver(
                () =>
                {
                    factoryCalls++;
                    throw new InvalidOperationException("The tracer factory should not be called.");
                },
                Mock.Of<IDatadogLogger>(),
                Mock.Of<IMetricsTelemetryCollector>());

            observer.SubscribeIfMatch(listener.Object).Should().BeNull();

            factoryCalls.Should().Be(0);
            listener.Verify(
                instance => instance.Subscribe(
                    It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                    It.IsAny<Predicate<string>>()),
                Times.Never);
        }

        [Fact]
        public async Task MatchingListenerEvaluatesDisabledActivationOnlyOnce()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var factoryCalls = 0;
            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Microsoft.AspNetCore");
            var observer = new LegacyAspNetCoreDiagnosticObserver(
                () =>
                {
                    factoryCalls++;
                    return tracer;
                },
                Mock.Of<IDatadogLogger>(),
                Mock.Of<IMetricsTelemetryCollector>());

            observer.SubscribeIfMatch(listener.Object).Should().BeNull();
            observer.SubscribeIfMatch(listener.Object).Should().BeNull();

            factoryCalls.Should().Be(1);
            listener.Verify(
                instance => instance.Subscribe(
                    It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                    It.IsAny<Predicate<string>>()),
                Times.Never);
        }

        [Fact]
        public void ActivationFailureIsCachedAndDoesNotEscape()
        {
            var factoryCalls = 0;
            var logger = new Mock<IDatadogLogger>();
            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Microsoft.AspNetCore");
            var observer = new LegacyAspNetCoreDiagnosticObserver(
                () =>
                {
                    factoryCalls++;
                    throw new InvalidOperationException("Test activation failure.");
                },
                logger.Object,
                Mock.Of<IMetricsTelemetryCollector>());

            observer.SubscribeIfMatch(listener.Object).Should().BeNull();
            observer.SubscribeIfMatch(listener.Object).Should().BeNull();

            factoryCalls.Should().Be(1);
            logger.Invocations.Count(invocation => invocation.Method.Name == nameof(IDatadogLogger.Error)).Should().Be(1);
            listener.Verify(
                instance => instance.Subscribe(
                    It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                    It.IsAny<Predicate<string>>()),
                Times.Never);
        }

        [Fact]
        public async Task SubscriptionFiltersEventsAtTheDiagnosticSource()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            Predicate<string> eventFilter = null;
            var listener = new Mock<IDiagnosticListener>();
            listener.SetupGet(instance => instance.Name).Returns("Microsoft.AspNetCore");
            listener.Setup(
                        instance => instance.Subscribe(
                            It.IsAny<IObserver<KeyValuePair<string, object>>>(),
                            It.IsAny<Predicate<string>>()))
                    .Callback<IObserver<KeyValuePair<string, object>>, Predicate<string>>((_, predicate) => eventFilter = predicate)
                    .Returns(Mock.Of<IDisposable>());
            var observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            using var subscription = observer.SubscribeIfMatch(listener.Object);

            observer.IsSubscriberEnabled().Should().BeTrue();
            subscription.Should().NotBeNull();
            eventFilter.Should().NotBeNull();
            eventFilter("Microsoft.AspNetCore.Hosting.HttpRequestIn").Should().BeTrue();
            eventFilter(StartEvent).Should().BeTrue();
            eventFilter(StopEvent).Should().BeTrue();
            eventFilter(HostingUnhandledExceptionEvent).Should().BeTrue();
            eventFilter(DiagnosticsUnhandledExceptionEvent).Should().BeTrue();
            eventFilter(MvcBeforeActionEvent).Should().BeTrue();
            eventFilter("Microsoft.AspNetCore.Mvc.AfterAction").Should().BeFalse();
        }

        [Fact]
        public async Task MvcAttributeRouteHasNamingPrecedenceAndUpdatesRootTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Request.Method = "post";
            var requestPayload = new { HttpContext = context };
            var actionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["controller"] = "Orders",
                ["action"] = "Details",
                ["area"] = "Admin",
            };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestScope = GetRequestState(context).RootScope;

            observer.OnNext(
                new KeyValuePair<string, object>(
                    MvcBeforeActionEvent,
                    CreateMvcBeforeActionPayload(context, "api/Orders/{id}", actionValues)));

            requestScope.Span.ResourceName.Should().Be("POST api/Orders/{id}");
            requestScope.Span.GetTag(Tags.AspNetCoreRoute).Should().Be("api/Orders/{id}");
            requestScope.Span.GetTag(Tags.HttpRoute).Should().Be("api/Orders/{id}");
            requestScope.Span.GetTag(Tags.AspNetCoreController).Should().Be("Orders");
            requestScope.Span.GetTag(Tags.AspNetCoreAction).Should().Be("Details");
            requestScope.Span.GetTag(Tags.AspNetCoreArea).Should().Be("Admin");

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Theory]
        [InlineData(null, "GET Orders/Details")]
        [InlineData("Admin", "GET Admin/Orders/Details")]
        public async Task MvcControllerActionRouteUpdatesRootName(string area, string expectedResourceName)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };
            var actionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["controller"] = "Orders",
                ["action"] = "Details",
            };
            if (area is not null)
            {
                actionValues["area"] = area;
            }

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestScope = GetRequestState(context).RootScope;

            observer.OnNext(
                new KeyValuePair<string, object>(
                    MvcBeforeActionEvent,
                    CreateMvcBeforeActionPayload(context, null, actionValues)));

            requestScope.Span.ResourceName.Should().Be(expectedResourceName);
            requestScope.Span.GetTag(Tags.AspNetCoreRoute).Should().Be(expectedResourceName.Substring(4));
            requestScope.Span.GetTag(Tags.AspNetCoreController).Should().Be("Orders");
            requestScope.Span.GetTag(Tags.AspNetCoreAction).Should().Be("Details");
            requestScope.Span.GetTag(Tags.AspNetCoreArea).Should().Be(area);

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Fact]
        public async Task MvcRouteDataValuesProvideControllerActionFallback()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };
            var routeDataValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["controller"] = "Catalog",
                ["action"] = "Index",
                ["area"] = "Store",
            };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestScope = GetRequestState(context).RootScope;

            observer.OnNext(
                new KeyValuePair<string, object>(
                    MvcBeforeActionEvent,
                    CreateMvcBeforeActionPayload(
                        context,
                        null,
                        new Dictionary<string, string>(),
                        routeDataValues)));

            requestScope.Span.ResourceName.Should().Be("GET Store/Catalog/Index");
            requestScope.Span.GetTag(Tags.AspNetCoreRoute).Should().Be("Store/Catalog/Index");
            requestScope.Span.GetTag(Tags.AspNetCoreController).Should().Be("Catalog");
            requestScope.Span.GetTag(Tags.AspNetCoreAction).Should().Be("Index");
            requestScope.Span.GetTag(Tags.AspNetCoreArea).Should().Be("Store");

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Fact]
        public async Task UnsupportedMvcActionDescriptorRetainsStartFallback()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };
            var mvcPayload = new
            {
                HttpContext = context,
                ActionDescriptor = new object(),
                RouteData = new FakeRouteData(),
            };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestState = GetRequestState(context);

            var action = () => observer.OnNext(new KeyValuePair<string, object>(MvcBeforeActionEvent, mvcPayload));

            action.Should().NotThrow();
            requestState.RootScope.Span.ResourceName.Should().Be("GET /baseline/mongo");
            requestState.RootScope.Span.GetTag(Tags.AspNetCoreRoute).Should().BeNull();
            requestState.RootScope.Span.IsFinished.Should().BeFalse();
            GetRequestState(context).Should().BeSameAs(requestState);

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Fact]
        public async Task DuplicateMvcEventsUpdateOnlyStoredRootScope()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };
            var actionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["controller"] = "Orders",
                ["action"] = "Details",
            };
            var mvcPayload = CreateMvcBeforeActionPayload(context, "api/Orders/{id}", actionValues);

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestState = GetRequestState(context);
            using (var childScope = tracer.StartActiveInternal("child"))
            {
                childScope.Span.ResourceName = "child-resource";

                observer.OnNext(new KeyValuePair<string, object>(MvcBeforeActionEvent, mvcPayload));
                observer.OnNext(new KeyValuePair<string, object>(MvcBeforeActionEvent, mvcPayload));

                tracer.ActiveScope.Should().BeSameAs(childScope);
                childScope.Span.ResourceName.Should().Be("child-resource");
                requestState.RootScope.Span.ResourceName.Should().Be("GET api/Orders/{id}");
                GetRequestState(context).Should().BeSameAs(requestState);
            }

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Fact]
        public async Task UnsupportedStartPayloadShapesDoNotCreateScope()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var invalidHeadersContext = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            invalidHeadersContext.Request.Headers = new object();
            object[] payloads =
            [
                new object(),
                new { HttpContext = new object() },
                new
                {
                    HttpContext = new
                    {
                        Items = new Dictionary<object, object>(),
                        Request = new object(),
                    },
                },
                new { HttpContext = invalidHeadersContext },
            ];

            foreach (var payload in payloads)
            {
                var action = () => observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

                action.Should().NotThrow();
                tracer.ActiveScope.Should().BeNull();
            }

            HasRequestState(invalidHeadersContext).Should().BeFalse();
        }

        [Fact]
        public async Task RepeatedIncompatiblePayloadsHaveBoundedDiagnostics()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var logger = new Mock<IDatadogLogger>();
            var metrics = new Mock<IMetricsTelemetryCollector>();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer, logger.Object, metrics.Object);
            var invalidPayload = new object();

            for (var i = 0; i < 5; i++)
            {
                observer.OnNext(new KeyValuePair<string, object>(StartEvent, invalidPayload));
                observer.OnNext(new KeyValuePair<string, object>(StopEvent, invalidPayload));
                observer.OnNext(new KeyValuePair<string, object>(HostingUnhandledExceptionEvent, invalidPayload));
                observer.OnNext(new KeyValuePair<string, object>(DiagnosticsUnhandledExceptionEvent, invalidPayload));
            }

            logger.Invocations.Count(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning)).Should().Be(4);
            metrics.Verify(
                collector => collector.RecordCountSharedIntegrationsError(
                    MetricTags.IntegrationName.AspNetCore,
                    MetricTags.InstrumentationError.DuckTyping,
                    1),
                Times.Exactly(4));
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task StoresExactScopeAndClosesItIdempotently()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(
                new FakeLegacyHeaders(
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["x-datadog-trace-id"] = new StringValues22(IncomingTraceId.ToString()),
                        ["x-datadog-parent-id"] = new StringValues22(IncomingParentId.ToString()),
                        ["x-datadog-sampling-priority"] = new StringValues22("1"),
                    }));
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

            var requestScope = GetRequestState(context).RootScope;
            requestScope.Should().BeSameAs(tracer.ActiveScope);
            requestScope.Span.TraceId.Should().Be(IncomingTraceId);
            requestScope.Span.Context.ParentId.Should().Be(IncomingParentId);

            await Task.Yield();
            using (var childScope = tracer.StartActiveInternal("mongodb.query"))
            {
                childScope.Span.TraceId.Should().Be(IncomingTraceId);
                childScope.Span.Context.ParentId.Should().Be(requestScope.Span.SpanId);
            }

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
            context.Response = new FakeHttpResponse { StatusCode = 503 };
            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            HasRequestState(context).Should().BeFalse();
            tracer.ActiveScope.Should().BeNull();
            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("200");
        }

        [Fact]
        public async Task PrivateRequestStateKeyDoesNotCollideWithApplicationItem()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var payload = new { HttpContext = context };
            var applicationValue = new object();
            const string FormerScopeKey = "__Datadog.LegacyAspNetCoreDiagnosticObserver.Scope";
            context.Items[FormerScopeKey] = applicationValue;

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

            var stateEntry = context.Items.Single(item => item.Value is LegacyAspNetCoreRequestState);
            stateEntry.Key.Should().NotBeOfType<string>();
            stateEntry.Value.Should().BeOfType<LegacyAspNetCoreRequestState>();
            context.Items[FormerScopeKey].Should().BeSameAs(applicationValue);

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            HasRequestState(context).Should().BeFalse();
            context.Items.Should().ContainSingle();
            context.Items[FormerScopeKey].Should().BeSameAs(applicationValue);
        }

        [Fact]
        public async Task DuplicateStartKeepsFirstRequestState()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var firstState = GetRequestState(context);

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

            GetRequestState(context).Should().BeSameAs(firstState);
            tracer.ActiveScope.Should().BeSameAs(firstState.RootScope);

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            firstState.RootScope.Span.IsFinished.Should().BeTrue();
            HasRequestState(context).Should().BeFalse();
        }

        [Fact]
        public async Task StopClosesStoredRequestScopeWhileChildIsActive()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var requestScope = GetRequestState(context).RootScope;
            var childScope = tracer.StartActiveInternal("child");

            try
            {
                observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

                requestScope.Span.IsFinished.Should().BeTrue();
                childScope.Span.IsFinished.Should().BeFalse();
                tracer.ActiveScope.Should().BeSameAs(childScope);
                HasRequestState(context).Should().BeFalse();
            }
            finally
            {
                childScope.Dispose();
                ((IScopeRawAccess)tracer.ScopeManager).Active = null;
            }
        }

        [Fact]
        public async Task ConcurrentRequestsKeepSeparateState()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var firstContext = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var secondContext = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            using var bothStarted = new Barrier(2);

            LegacyAspNetCoreRequestState RunRequest(FakeHttpContext context)
            {
                var payload = new { HttpContext = context };
                observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
                var state = GetRequestState(context);

                bothStarted.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();
                state.RootScope.Should().BeSameAs(tracer.ActiveScope);

                observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
                tracer.ActiveScope.Should().BeNull();
                return state;
            }

            var states = await Task.WhenAll(
                             Task.Run(() => RunRequest(firstContext)),
                             Task.Run(() => RunRequest(secondContext)));

            states[0].Should().NotBeSameAs(states[1]);
            states[0].RootScope.Should().NotBeSameAs(states[1].RootScope);
            states[0].RootScope.Span.TraceId.Should().NotBe(states[1].RootScope.Span.TraceId);
            states.Should().OnlyContain(state => state.RootScope.Span.IsFinished);
            HasRequestState(firstContext).Should().BeFalse();
            HasRequestState(secondContext).Should().BeFalse();
        }

        [Fact]
        public async Task StopDisposesStoredScopeWhenResponseShapeIsUnsupported()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            HasRequestState(context).Should().BeTrue();
            tracer.ActiveScope.Should().NotBeNull();

            context.Response = new object();
            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            HasRequestState(context).Should().BeFalse();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task RepeatedUnsupportedStopResponsesHaveBoundedDiagnosticsAndCloseScopes()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var logger = new Mock<IDatadogLogger>();
            var metrics = new Mock<IMetricsTelemetryCollector>();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer, logger.Object, metrics.Object);

            for (var i = 0; i < 5; i++)
            {
                var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
                var payload = new { HttpContext = context };
                observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
                var requestScope = GetRequestState(context).RootScope;
                context.Response = new object();

                observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

                requestScope.Span.IsFinished.Should().BeTrue();
                HasRequestState(context).Should().BeFalse();
                tracer.ActiveScope.Should().BeNull();
            }

            logger.Invocations.Count(invocation => invocation.Method.Name == nameof(IDatadogLogger.Warning)).Should().Be(1);
            metrics.Verify(
                collector => collector.RecordCountSharedIntegrationsError(
                    MetricTags.IntegrationName.AspNetCore,
                    MetricTags.InstrumentationError.DuckTyping,
                    1),
                Times.Once);
        }

        [Fact]
        public async Task StopDisposesStoredScopeWhenResponseMemberIsMissing()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var startPayload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, startPayload));
            var requestScope = GetRequestState(context).RootScope;
            var stopPayload = new { HttpContext = new ItemsOnlyHttpContext(context.Items) };

            var action = () => observer.OnNext(new KeyValuePair<string, object>(StopEvent, stopPayload));

            action.Should().NotThrow();
            requestScope.Span.IsFinished.Should().BeTrue();
            HasRequestState(context).Should().BeFalse();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task UnsupportedExceptionContextDoesNotAffectStoredScope()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };
            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));
            var requestScope = GetRequestState(context).RootScope;
            var exceptionPayload = new { HttpContext = new object(), Exception = new InvalidOperationException("ignored") };

            var action = () => observer.OnNext(new KeyValuePair<string, object>(HostingUnhandledExceptionEvent, exceptionPayload));

            action.Should().NotThrow();
            requestScope.Span.Error.Should().BeFalse();
            requestScope.Span.IsFinished.Should().BeFalse();

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));
        }

        [Fact]
        public async Task StartDisposesCreatedScopeWhenStateStorageThrows()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var items = new ThrowingSetItemsDictionary();
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Items = items;
            var payload = new { HttpContext = context };
            var startMethod = typeof(LegacyAspNetCoreDiagnosticObserver)
                             .GetMethod("OnHostingHttpRequestInStart", BindingFlags.Instance | BindingFlags.NonPublic);

            var action = () => startMethod.Invoke(observer, [payload]);

            action.Should().Throw<TargetInvocationException>()
                  .WithInnerException<InvalidOperationException>();
            var attemptedState = items.AttemptedValue.Should().BeOfType<LegacyAspNetCoreRequestState>().Subject;
            attemptedState.RootScope.Span.IsFinished.Should().BeTrue();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task HandlerDisposesCreatedScopeWhenRequestEnrichmentThrows()
        {
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.HeaderTags, "x-throw-after-scope:test.throw" },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var headers = new ThrowingLegacyHeaders("x-throw-after-scope");
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var request = context.Request.DuckCast<LegacyAspNetCoreDiagnosticObserver.LegacyAspNetCoreHttpRequestStruct>();
            var headersAdapter = new LegacyAspNetCoreHeadersCollectionAdapter(headers);
            var handler = new LegacyAspNetCoreHttpRequestHandler(DatadogLogging.GetLoggerFor<LegacyAspNetCoreDiagnosticObserverTests>());

            var action = () => handler.StartAspNetCorePipelineScope(tracer, request, headersAdapter);

            action.Should().Throw<InvalidOperationException>();
            tracer.ActiveScope.Should().BeNull();
        }

        [Theory]
        [InlineData(HostingUnhandledExceptionEvent)]
        [InlineData(DiagnosticsUnhandledExceptionEvent)]
        public async Task UnhandledExceptionMarksStoredScope(string eventName)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));

            var requestScope = GetRequestState(context).RootScope;
            var exception = new InvalidOperationException("Unhandled request failure");
            var exceptionPayload = new { HttpContext = context, Exception = exception };

            observer.OnNext(new KeyValuePair<string, object>(eventName, exceptionPayload));

            requestScope.Span.Error.Should().BeTrue();
            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("500");
            requestScope.Span.GetTag(Tags.ErrorMsg).Should().Be(exception.Message);
            requestScope.Span.GetTag(Tags.ErrorType).Should().Contain(nameof(InvalidOperationException));

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));

            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("500");
        }

        [Theory]
        [InlineData(HostingUnhandledExceptionEvent)]
        [InlineData(DiagnosticsUnhandledExceptionEvent)]
        public async Task BadHttpRequestExceptionUsesNonPublicCaseInsensitiveStatusAndStopPreservesIt(string eventName)
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var requestPayload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, requestPayload));

            var requestScope = GetRequestState(context).RootScope;
            var exception = new FakeBadHttpRequestException(statusCode: 413);
            var exceptionPayload = new { HttpContext = context, Exception = exception };

            observer.OnNext(new KeyValuePair<string, object>(eventName, exceptionPayload));

            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("413");

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, requestPayload));

            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("413");
            requestScope.Span.IsFinished.Should().BeTrue();
        }

        [Fact]
        public async Task MergesAndTagsExtractedBaggage()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(
                new FakeLegacyHeaders(
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["baggage"] = new StringValues22("user.id=legacy-user"),
                    }));
            var payload = new { HttpContext = context };
            var previousBaggage = Baggage.Current;

            try
            {
                Baggage.Current = new Baggage();
                observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

                var requestScope = GetRequestState(context).RootScope;
                Baggage.Current["user.id"].Should().Be("legacy-user");
                requestScope.Span.GetTag("baggage.user.id").Should().Be("legacy-user");

                observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
            }
            finally
            {
                if (HasRequestState(context))
                {
                    observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
                }

                Baggage.Current = previousBaggage;
            }
        }

        [Theory]
        [InlineData(true, "http://localhost/baseline/mongo?item=42&<redacted>")]
        [InlineData(false, "http://localhost/baseline/mongo")]
        public async Task AppliesConfiguredHttpMetadata(bool reportQueryString, string expectedUrl)
        {
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.QueryStringReportingEnabled, reportQueryString.ToString() },
                        { ConfigurationKeys.HeaderTags, "x-legacy-test-header:legacy.request.header" },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(
                new FakeLegacyHeaders(
                    new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["x-legacy-test-header"] = new StringValues22("header-value"),
                    }));
            context.Request.QueryString = new FakeQueryString { Value = "?item=42&token=secret" };
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

            var requestScope = GetRequestState(context).RootScope;
            requestScope.Span.GetTag("http.url").Should().Be(expectedUrl);
            requestScope.Span.GetTag("legacy.request.header").Should().Be("header-value");

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
        }

        [Fact]
        public async Task ManuallyErroredScopeStillRecordsResponseStatus()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

            var requestScope = GetRequestState(context).RootScope;
            requestScope.Span.Error = true;

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("200");
        }

        [Fact]
        public async Task StopAddsConfiguredResponseHeaderTags()
        {
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.HeaderTags, "x-response-single:response.single,x-response-multi:response.multi,x-response-default,x-response-missing:response.missing" },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Response = new FakeHttpResponse
            {
                StatusCode = 200,
                Headers = new HeaderDictionary
                {
                    ["x-response-single"] = "single-value",
                    ["x-response-multi"] = new StringValues([string.Empty, "multi-value"]),
                    ["x-response-default"] = "default-value",
                },
            };
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var requestScope = GetRequestState(context).RootScope;

            observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            requestScope.Span.GetTag("response.single").Should().Be("single-value");
            requestScope.Span.GetTag("response.multi").Should().Be("multi-value");
            requestScope.Span.GetTag("http.response.headers.x-response-default").Should().Be("default-value");
            requestScope.Span.GetTag("response.missing").Should().BeNull();
            requestScope.Span.IsFinished.Should().BeTrue();
        }

        [Fact]
        public async Task StopDoesNotCreateResponseHeaderProxyWithoutHeaderTags()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Response = new FakeHttpResponse { StatusCode = 204, Headers = new object() };
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var requestScope = GetRequestState(context).RootScope;

            var action = () => observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            action.Should().NotThrow();
            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("204");
            requestScope.Span.IsFinished.Should().BeTrue();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task UnsupportedResponseHeadersStillRecordStatusAndCloseScope()
        {
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.HeaderTags, "x-response:test.response" },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Response = new FakeHttpResponse { StatusCode = 202, Headers = new object() };
            var payload = new { HttpContext = context };

            observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var requestScope = GetRequestState(context).RootScope;

            var action = () => observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            action.Should().NotThrow();
            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("202");
            requestScope.Span.GetTag("test.response").Should().BeNull();
            requestScope.Span.IsFinished.Should().BeTrue();
            HasRequestState(context).Should().BeFalse();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public async Task StopClosesStoredScopeWhenResponseHeaderTaggingThrows()
        {
            var settings = new TracerSettings(
                new NameValueConfigurationSource(
                    new NameValueCollection
                    {
                        { ConfigurationKeys.HeaderTags, "x-throw-on-response:test.response" },
                    }));
            await using var tracer = TracerHelper.CreateWithFakeAgent(settings);
            var observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            var context = CreateContext(new FakeLegacyHeaders(new Dictionary<string, object>()));
            context.Response = new FakeHttpResponse
            {
                StatusCode = 202,
                Headers = new ThrowingLegacyHeaders("x-throw-on-response"),
            };
            var payload = new { HttpContext = context };
            ((IObserver<KeyValuePair<string, object>>)observer).OnNext(new KeyValuePair<string, object>(StartEvent, payload));
            var requestScope = GetRequestState(context).RootScope;

            var action = () => ((IObserver<KeyValuePair<string, object>>)observer).OnNext(new KeyValuePair<string, object>(StopEvent, payload));

            action.Should().NotThrow();
            requestScope.Span.GetTag(Tags.HttpStatusCode).Should().Be("202");
            requestScope.Span.GetTag("test.response").Should().BeNull();
            requestScope.Span.IsFinished.Should().BeTrue();
            HasRequestState(context).Should().BeFalse();
            tracer.ActiveScope.Should().BeNull();
        }

        [Fact]
        public void HeaderAdapterReadsAspNetCore21StringValuesShape()
        {
            var headers = new FakeLegacyHeaders(
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["traceparent"] = new StringValues21("first", "second"),
                });

            AssertHeaderValues(headers);
        }

        [Fact]
        public void HeaderAdapterReadsAspNetCore22StringValuesShape()
        {
            var headers = new FakeLegacyHeaders(
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["traceparent"] = new StringValues22("first", "second"),
                });

            AssertHeaderValues(headers);
        }

        [Fact]
        public void HeaderAdapterReadsPublicHeaderDictionaryIndexer()
        {
            IHeaderDictionary headers = new HeaderDictionary
            {
                ["x-single"] = "value",
                ["traceparent"] = new StringValues(["first", "second"]),
            };

            var proxy = headers.DuckCast<ILegacyAspNetCoreHeaders>();
            new LegacyAspNetCoreHeadersCollectionAdapter(proxy).GetValues("x-single").Should().Equal("value");
            AssertHeaderValues(proxy);
        }

        [Fact]
        public void HeaderAdapterReadsKestrelStyleExplicitIndexerFromBaseType()
        {
            var headers = new ExplicitlyImplementedHeaderDictionary();
            headers.Set("x-single", "value");
            headers.Set("traceparent", new StringValues(["first", "second"]));

            var proxy = headers.DuckCast<ILegacyAspNetCoreHeaders>();
            new LegacyAspNetCoreHeadersCollectionAdapter(proxy).GetValues("x-single").Should().Equal("value");
            AssertHeaderValues(proxy);
        }

        private static object CreateMvcBeforeActionPayload(
            FakeHttpContext context,
            string routeTemplate,
            IDictionary<string, string> actionDescriptorValues,
            IDictionary<string, object> routeDataValues = null)
        {
            return new
            {
                HttpContext = context,
                ActionDescriptor = new FakeActionDescriptor
                {
                    AttributeRouteInfo = routeTemplate is null ? null : new FakeAttributeRouteInfo { Template = routeTemplate },
                    RouteValues = actionDescriptorValues,
                },
                RouteData = new FakeRouteData
                {
                    Values = routeDataValues ?? new Dictionary<string, object>(),
                },
            };
        }

        private static FakeHttpContext CreateContext(FakeLegacyHeaders headers)
        {
            return new FakeHttpContext
            {
                Request = new FakeHttpRequest
                {
                    Method = "GET",
                    Scheme = "http",
                    Host = new FakeHostString { Value = "localhost" },
                    PathBase = new FakePathString { Value = string.Empty },
                    Path = new FakePathString { Value = "/baseline/mongo" },
                    QueryString = new FakeQueryString { Value = string.Empty },
                    Headers = headers,
                },
                Response = new FakeHttpResponse { StatusCode = 200 },
            };
        }

        private static LegacyAspNetCoreRequestState GetRequestState(FakeHttpContext context)
        {
            var states = context.Items.Values.OfType<LegacyAspNetCoreRequestState>().ToArray();
            states.Should().ContainSingle();
            return states[0];
        }

        private static bool HasRequestState(FakeHttpContext context)
            => context.Items.Values.OfType<LegacyAspNetCoreRequestState>().Any();

        private static void AssertHeaderValues(ILegacyAspNetCoreHeaders headers)
        {
            var adapter = new LegacyAspNetCoreHeadersCollectionAdapter(headers);

            adapter.GetValues("traceparent").Should().Equal("first", "second");
            adapter.GetValues("missing").Should().BeEmpty();
        }

        private struct FakeHostString
        {
            public string Value { get; set; }
        }

        private struct FakePathString
        {
            public string Value { get; set; }
        }

        private struct FakeQueryString
        {
            public string Value { get; set; }
        }

        private readonly struct StringValues21 : IEnumerable<string>
        {
            private readonly string[] _values;

            public StringValues21(params string[] values)
            {
                _values = values;
            }

            public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)(_values ?? [])).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private readonly struct StringValues22 : IEnumerable<string>
        {
            private readonly string[] _values;

            public StringValues22(params string[] values)
            {
                _values = values;
            }

            public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)(_values ?? [])).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class FakeHttpContext
        {
            public FakeHttpRequest Request { get; set; }

            public object Response { get; set; }

            public IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        }

        private sealed class ItemsOnlyHttpContext
        {
            public ItemsOnlyHttpContext(IDictionary<object, object> items)
            {
                Items = items;
            }

            public IDictionary<object, object> Items { get; }
        }

        private sealed class FakeHttpRequest
        {
            public string Method { get; set; }

            public string Scheme { get; set; }

            public FakeHostString Host { get; set; }

            public FakePathString PathBase { get; set; }

            public FakePathString Path { get; set; }

            public FakeQueryString QueryString { get; set; }

            public object Headers { get; set; }
        }

        private sealed class FakeHttpResponse
        {
            public int StatusCode { get; set; }

            public object Headers { get; set; }
        }

        private sealed class FakeBadHttpRequestException : Exception
        {
            public FakeBadHttpRequestException(int statusCode)
            {
                STATUSCODE = statusCode;
            }

            private int STATUSCODE { get; }
        }

        private sealed class FakeActionDescriptor
        {
            public object AttributeRouteInfo { get; set; }

            public IDictionary<string, string> RouteValues { get; set; }
        }

        private sealed class FakeAttributeRouteInfo
        {
            public string Template { get; set; }
        }

        private sealed class FakeRouteData
        {
            public IDictionary<string, object> Values { get; set; } = new Dictionary<string, object>();
        }

        private sealed class FakeLegacyHeaders : ILegacyAspNetCoreHeaders
        {
            private readonly IReadOnlyDictionary<string, object> _headers;

            public FakeLegacyHeaders(IReadOnlyDictionary<string, object> headers)
            {
                _headers = headers;
            }

            public IEnumerable<string> this[string name]
                => _headers.TryGetValue(name, out var values) ? (IEnumerable<string>)values : [];
        }

        private sealed class ThrowingLegacyHeaders : ILegacyAspNetCoreHeaders
        {
            private readonly string _throwingHeader;

            public ThrowingLegacyHeaders(string throwingHeader)
            {
                _throwingHeader = throwingHeader;
            }

            public IEnumerable<string> this[string name]
                => string.Equals(name, _throwingHeader, StringComparison.OrdinalIgnoreCase)
                       ? throw new InvalidOperationException("Header access failed after scope creation.")
                       : [];
        }

        private sealed class ThrowingSetItemsDictionary : IDictionary<object, object>
        {
            public object AttemptedValue { get; private set; }

            public ICollection<object> Keys => throw new NotSupportedException();

            public ICollection<object> Values => throw new NotSupportedException();

            public int Count => 0;

            public bool IsReadOnly => false;

            public object this[object key]
            {
                get => throw new NotSupportedException();
                set
                {
                    AttemptedValue = value;
                    throw new InvalidOperationException("State storage failed.");
                }
            }

            public void Add(object key, object value) => throw new NotSupportedException();

            public void Add(KeyValuePair<object, object> item) => throw new NotSupportedException();

            public void Clear() => throw new NotSupportedException();

            public bool Contains(KeyValuePair<object, object> item) => false;

            public bool ContainsKey(object key) => false;

            public void CopyTo(KeyValuePair<object, object>[] array, int arrayIndex) => throw new NotSupportedException();

            public IEnumerator<KeyValuePair<object, object>> GetEnumerator() => throw new NotSupportedException();

            public bool Remove(object key) => false;

            public bool Remove(KeyValuePair<object, object> item) => false;

            public bool TryGetValue(object key, out object value)
            {
                value = null;
                return false;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Reproduces the Kestrel 2.x shape where the concrete request-header type inherits the
        /// explicit IHeaderDictionary implementation from an abstract base class.
        /// </summary>
        private sealed class ExplicitlyImplementedHeaderDictionary : KestrelStyleHeaderDictionaryBase
        {
        }

        private abstract class KestrelStyleHeaderDictionaryBase : IHeaderDictionary
        {
            private readonly Dictionary<string, StringValues> _store = new(StringComparer.OrdinalIgnoreCase);

            int ICollection<KeyValuePair<string, StringValues>>.Count => _store.Count;

            bool ICollection<KeyValuePair<string, StringValues>>.IsReadOnly => false;

            ICollection<string> IDictionary<string, StringValues>.Keys => _store.Keys;

            ICollection<StringValues> IDictionary<string, StringValues>.Values => _store.Values;

            long? IHeaderDictionary.ContentLength
            {
                get => null;
                set { }
            }

            StringValues IHeaderDictionary.this[string key]
            {
                get
                {
                    _store.TryGetValue(key, out var value);
                    return value;
                }

                set => _store[key] = value;
            }

            StringValues IDictionary<string, StringValues>.this[string key]
            {
                get => _store[key];
                set => _store[key] = value;
            }

            public void Set(string key, StringValues value) => _store[key] = value;

            void IDictionary<string, StringValues>.Add(string key, StringValues value) => _store.Add(key, value);

            bool IDictionary<string, StringValues>.ContainsKey(string key) => _store.ContainsKey(key);

            bool IDictionary<string, StringValues>.Remove(string key) => _store.Remove(key);

            bool IDictionary<string, StringValues>.TryGetValue(string key, out StringValues value) => _store.TryGetValue(key, out value);

            void ICollection<KeyValuePair<string, StringValues>>.Add(KeyValuePair<string, StringValues> item) => _store.Add(item.Key, item.Value);

            void ICollection<KeyValuePair<string, StringValues>>.Clear() => _store.Clear();

            bool ICollection<KeyValuePair<string, StringValues>>.Contains(KeyValuePair<string, StringValues> item) => _store.ContainsKey(item.Key);

            void ICollection<KeyValuePair<string, StringValues>>.CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, StringValues>>)_store).CopyTo(array, arrayIndex);

            bool ICollection<KeyValuePair<string, StringValues>>.Remove(KeyValuePair<string, StringValues> item) => _store.Remove(item.Key);

            IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator() => _store.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _store.GetEnumerator();
        }
    }
}

#endif
