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
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using Datadog.Trace.Tests.PlatformHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class LegacyAspNetCoreDiagnosticObserverTests
{
    private const string StartEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Start";
    private const string StopEvent = "Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop";
    private const string HostingUnhandledExceptionEvent = "Microsoft.AspNetCore.Hosting.UnhandledException";
    private const string DiagnosticsUnhandledExceptionEvent = "Microsoft.AspNetCore.Diagnostics.UnhandledException";
    private const string MvcBeforeActionEvent = "Microsoft.AspNetCore.Mvc.BeforeAction";

    [Fact]
    public async Task MvcAttributeRouteHasNamingPrecedenceAndUpdatesRootTags()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext();
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
        var context = CreateContext();
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
        var context = CreateContext();
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
    public async Task DuplicateMvcEventsUpdateOnlyStoredRootScope()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext();
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
    public async Task ConcurrentRequestsKeepSeparateState()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var firstContext = CreateContext();
        var secondContext = CreateContext();
        using var bothStarted = new Barrier(2);

        LegacyAspNetCoreRequestState RunRequest(HttpContext context)
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
    }

    [Theory]
    [InlineData(HostingUnhandledExceptionEvent)]
    [InlineData(DiagnosticsUnhandledExceptionEvent)]
    public async Task UnhandledExceptionMarksStoredScope(string eventName)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext();
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
        var context = CreateContext();
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

    [Fact(Skip = "Using Baggage.Current means this will be flaky in CI")]
    public async Task MergesAndTagsExtractedBaggage()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext(
            new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
            {
                ["baggage"] = new("user.id=legacy-user"),
            });
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
            Baggage.Current = previousBaggage;
        }
    }

    [Theory]
    [InlineData(true, "http://localhost/baseline/sql?item=42&<redacted>")]
    [InlineData(false, "http://localhost/baseline/sql")]
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
                new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x-legacy-test-header"] = new("header-value"),
                });
        context.Request.QueryString = new("?item=42&token=secret");
        var payload = new { HttpContext = context };

        observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

        var requestScope = GetRequestState(context).RootScope;
        requestScope.Span.GetTag("http.url").Should().Be(expectedUrl);
        requestScope.Span.GetTag("legacy.request.header").Should().Be("header-value");

        observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("single", null, "single")]
    [InlineData("first", "second", "first,second")]
    public async Task JoinsUserAgentValues(
        string first,
        string second,
        string expectedUserAgent)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        var headerValues = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        if (first is not null)
        {
            string[] values = second is null ? [first] : [first, second];
            headerValues[HttpHeaderNames.UserAgent] = new StringValues(values);
        }

        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext(headerValues);
        var payload = new { HttpContext = context };

        observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

        var requestScope = GetRequestState(context).RootScope;
        requestScope.Span.GetTag(Tags.HttpUserAgent).Should().Be(expectedUserAgent);

        observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
    }

    [Theory]
    [MemberData(nameof(AspNetCoreHttpUrlTestData.EscapedPaths), MemberType = typeof(AspNetCoreHttpUrlTestData))]
    public async Task EscapesDecodedPathValuesInHttpUrlAndResource(string pathBase, string path, string expectedUrl, string expectedResourceName)
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext();
        context.Request.PathBase = pathBase;
        context.Request.Path = path;
        var payload = new { HttpContext = context };

        observer.OnNext(new KeyValuePair<string, object>(StartEvent, payload));

        var requestScope = GetRequestState(context).RootScope;
        requestScope.Span.GetTag("http.url").Should().Be(expectedUrl);
        requestScope.Span.ResourceName.Should().Be(expectedResourceName);

        observer.OnNext(new KeyValuePair<string, object>(StopEvent, payload));
    }

    [Fact]
    public async Task ManuallyErroredScopeStillRecordsResponseStatus()
    {
        await using var tracer = TracerHelper.CreateWithFakeAgent();
        IObserver<KeyValuePair<string, object>> observer = new LegacyAspNetCoreDiagnosticObserver(tracer);
        var context = CreateContext();
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
        var context = CreateContext();
        context.Response.Headers["x-response-single"] = "single-value";
        context.Response.Headers["x-response-multi"] = new StringValues([string.Empty, "multi-value"]);
        context.Response.Headers["x-response-default"] = "default-value";
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
        // TODO: use the real type
        var headers = new ExplicitlyImplementedHeaderDictionary();
        headers.Set("x-single", "value");
        headers.Set("traceparent", new StringValues(["first", "second"]));

        var proxy = headers.DuckCast<ILegacyAspNetCoreHeaders>();
        new LegacyAspNetCoreHeadersCollectionAdapter(proxy).GetValues("x-single").Should().Equal("value");
        AssertHeaderValues(proxy);
    }

    private static object CreateMvcBeforeActionPayload(
        HttpContext context,
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

    private static HttpContext CreateContext(Dictionary<string, StringValues> headers = null)
    {
        var context = new DefaultHttpContext
        {
            Request =
            {
                Method = "GET",
                Scheme = "http",
                Host = new HostString("localhost"),
                PathBase = new PathString(string.Empty),
                Path = new PathString("/baseline/sql"),
                QueryString = new QueryString(string.Empty)
            },
            Response =
            {
                StatusCode = 200,
            }
        };
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                context.Request.Headers[header.Key] = header.Value;
            }
        }

        return context;
    }

    private static LegacyAspNetCoreRequestState GetRequestState(HttpContext context)
    {
        var states = context.Items.Values.OfType<LegacyAspNetCoreRequestState>().ToArray();
        states.Should().ContainSingle();
        return states[0];
    }

    private static void AssertHeaderValues(ILegacyAspNetCoreHeaders headers)
    {
        var adapter = new LegacyAspNetCoreHeadersCollectionAdapter(headers);

        adapter.GetValues("traceparent").Should().Equal("first", "second");
        adapter.GetValues("missing").Should().BeEmpty();
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
#endif
