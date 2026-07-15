// <copyright file="LegacyAspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
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

            Instrumentation.ShouldStartLegacyAspNetCoreDiagnosticObserver(tracer).Should().Be(expected);
        }

        [Fact]
        public async Task SubscriptionFiltersEventsAtTheDiagnosticSource()
        {
            await using var tracer = TracerHelper.CreateWithFakeAgent();
            var observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
            using var listener = new DiagnosticListener("Microsoft.AspNetCore");
            using var subscription = observer.SubscribeIfMatch(listener.DuckCast<IDiagnosticListener>());

            observer.IsSubscriberEnabled().Should().BeTrue();
            subscription.Should().NotBeNull();
            listener.IsEnabled("Microsoft.AspNetCore.Hosting.HttpRequestIn").Should().BeTrue();
            listener.IsEnabled(StartEvent).Should().BeTrue();
            listener.IsEnabled(StopEvent).Should().BeTrue();
            listener.IsEnabled(HostingUnhandledExceptionEvent).Should().BeTrue();
            listener.IsEnabled(DiagnosticsUnhandledExceptionEvent).Should().BeTrue();
            listener.IsEnabled("Microsoft.AspNetCore.Mvc.BeforeAction").Should().BeFalse();
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

            public IDictionary<object, object> Items { get; } = new Dictionary<object, object>();
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
