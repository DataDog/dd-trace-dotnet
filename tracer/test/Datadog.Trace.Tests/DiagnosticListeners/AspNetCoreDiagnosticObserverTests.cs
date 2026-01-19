// <copyright file="AspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class AspNetCoreDiagnosticObserverTests
    {
        [Fact]
        public async Task CompleteDiagnosticObserverTest()
        {
            await using var tracer = GetTracer();

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            using var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var (security, iast) = GetSecurity();
            var spanCodeOrigin = GetSpanCodeOrigin();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver(tracer, security, iast, spanCodeOrigin) };
            string retValue = null;

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                DiagnosticManager.Instance = diagnosticManager;
                retValue = await client.GetStringAsync("/Home");
                try
                {
                    await client.GetStringAsync("/Home/error");
                }
                catch { }
                DiagnosticManager.Instance = null;
            }

            _ = retValue;
        }

        [Theory]
        [CombinatorialData]
        public async Task HttpRequestIn_PopulateSpan(bool hasResourceBasedSamplingRules)
        {
            await using var tracer = GetTracer(hasResourceBasedSamplingRules);
            var (security, iast) = GetSecurity();
            var spanCodeOrigin = GetSpanCodeOrigin();

            IObserver<KeyValuePair<string, object>> observer = new AspNetCoreDiagnosticObserver(tracer, security, iast, spanCodeOrigin);

            var context = new HostingApplication.Context { HttpContext = GetHttpContext() };

            observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context));

            var scope = tracer.ActiveScope;

            Assert.NotNull(scope);

            var span = scope.Span;

            Assert.NotNull(span);

            Assert.Equal("aspnet_core.request", span.OperationName);
            Assert.Equal("aspnet_core", span.GetTag(Tags.InstrumentationName));
            Assert.Equal(SpanTypes.Web, span.Type);
            Assert.Equal(SpanKinds.Server, span.GetTag(Tags.SpanKind));
            Assert.Equal("GET", span.GetTag(Tags.HttpMethod));
            Assert.Equal("localhost", span.GetTag(Tags.HttpRequestHeadersHost));
            Assert.Equal("http://localhost/home/1/action", span.GetTag(Tags.HttpUrl));

            // Resource isn't populated until request end, unless we have resource-based sampling rules
            if (!hasResourceBasedSamplingRules)
            {
                observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context));
            }

            Assert.Equal("GET /home/?/action", span.ResourceName);
        }

#if NET6_0_OR_GREATER
        [Fact]
        public async Task CompleteSingleSpanDiagnosticObserverTest()
        {
            await using var tracer = GetTracer();

            var builder = new WebHostBuilder()
               .UseStartup<Startup>();

            using var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var (security, iast) = GetSecurity();
            var spanCodeOrigin = GetSpanCodeOrigin();
            var observers = new List<DiagnosticObserver> { new SingleSpanAspNetCoreDiagnosticObserver(tracer, security, iast, spanCodeOrigin) };
            string retValue = null;

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                DiagnosticManager.Instance = diagnosticManager;
                retValue = await client.GetStringAsync("/Home");
                try
                {
                    await client.GetStringAsync("/Home/error");
                }
                catch { }
                DiagnosticManager.Instance = null;
            }

            _ = retValue;
        }

        [Theory]
        [CombinatorialData]
        public async Task HttpRequestIn_SingleSpanPopulateSpan(bool hasResourceBasedSamplingRules)
        {
            await using var tracer = GetTracer(hasResourceBasedSamplingRules);
            var (security, iast) = GetSecurity();
            var spanCodeOrigin = GetSpanCodeOrigin();

            IObserver<KeyValuePair<string, object>> observer = new SingleSpanAspNetCoreDiagnosticObserver(tracer, security, iast, spanCodeOrigin);

            var context = new HostingApplication.Context { HttpContext = GetHttpContext() };

            observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context));

            var scope = tracer.ActiveScope;

            Assert.NotNull(scope);

            var span = scope.Span;

            Assert.NotNull(span);

            Assert.Equal("aspnet_core.request", span.OperationName);
            Assert.Equal("aspnet_core", span.GetTag(Tags.InstrumentationName));
            Assert.Equal(SpanTypes.Web, span.Type);
            Assert.Equal(SpanKinds.Server, span.GetTag(Tags.SpanKind));
            Assert.Equal("GET", span.GetTag(Tags.HttpMethod));
            Assert.Equal("localhost", span.GetTag(Tags.HttpRequestHeadersHost));
            Assert.Equal("http://localhost/home/1/action", span.GetTag(Tags.HttpUrl));

            // Resource isn't populated until request end, unless we have resource-based sampling rules
            if (!hasResourceBasedSamplingRules)
            {
                observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context));
            }

            Assert.Equal("GET /home/?/action", span.ResourceName);
        }
#endif

        private static ScopedTracer GetTracer(bool hasResourceBasedSamplingRules = false)
        {
            TracerSettings settings;
            if (hasResourceBasedSamplingRules)
            {
                settings = TracerSettings.Create(new()
                {
                    { ConfigurationKeys.CustomSamplingRules, """[{"sample_rate":0.0, "service":"*", "resource":"GET /status-code/?"}]""" },
                    { ConfigurationKeys.CustomSamplingRulesFormat, SamplingRulesFormat.Glob },
                });
            }
            else
            {
                settings = new TracerSettings();
            }

            var tracer = TracerHelper.CreateWithFakeAgent(settings);
            tracer.TracerManager.PerTraceSettings.HasResourceBasedSamplingRule.Should().Be(hasResourceBasedSamplingRules);
            return tracer;
        }

        private static (Security Security, Iast.Iast Iast) GetSecurity()
        {
            var settings = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.AppSec.Enabled, "0" },
                { ConfigurationKeys.Iast.Enabled, "0" },
            });
            // This still uses a _bunch_ of shared state. Ideally we should pass that in instead of accessing statics
            var security = new Security(
                new SecuritySettings(settings, NullConfigurationTelemetry.Instance),
                rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>());
            var iast = new Iast.Iast(new IastSettings(NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance), NullDiscoveryService.Instance);
            return (security, iast);
        }

        private static SpanCodeOrigin GetSpanCodeOrigin()
        {
            var settings = new NameValueConfigurationSource(new()
            {
                { ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "0" },
            });

            var co = new SpanCodeOrigin(new DebuggerSettings(settings, new NullConfigurationTelemetry()));
            return co;
        }

        private static HttpContext GetHttpContext()
        {
            var httpContext = new DefaultHttpContext();

            httpContext.Request.Headers.Add("hello", "hello");
            httpContext.Request.Headers.Add("world", "world");

            httpContext.Request.Host = new HostString("localhost");
            httpContext.Request.Scheme = "http";
            httpContext.Request.Path = "/home/1/action";
            httpContext.Request.Method = "GET";

            return httpContext;
        }

        private class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvc();
            }

            public void Configure(IApplicationBuilder builder)
            {
                builder.UseMvcWithDefaultRoute();
            }
        }
    }

    /// <summary>
    /// Simple controller used for the aspnetcore test
    /// </summary>
#pragma warning disable SA1402 // File may only contain a single class
    public class HomeController : Controller
    {
        public async Task<string> Index()
        {
            await Task.Yield();
            return "Hello world";
        }

        public void Error()
        {
            throw new Exception();
        }
    }
}

#endif
