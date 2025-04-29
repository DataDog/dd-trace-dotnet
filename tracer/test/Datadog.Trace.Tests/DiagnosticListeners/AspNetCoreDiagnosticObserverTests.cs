// <copyright file="AspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class AspNetCoreDiagnosticObserverTests
    {
        [Fact]
        public async Task<string> CompleteDiagnosticObserverTest()
        {
            TracerRestorerAttribute.SetTracer(GetTracer());

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var tracer = GetTracer();
            var (security, iast) = GetSecurity();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver(tracer, security, iast, null) };
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

            return retValue;
        }

        [Fact]
        public void HttpRequestIn_PopulateSpan()
        {
            var tracer = GetTracer();
            var (security, iast) = GetSecurity();

            IObserver<KeyValuePair<string, object>> observer = new AspNetCoreDiagnosticObserver(tracer, security, iast, null);

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

            // Resource isn't populated until request end
            observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Stop", context));
            Assert.Equal("GET /home/?/action", span.ResourceName);
        }

        private static Tracer GetTracer()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ITraceSampler>();

            return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
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
