// <copyright file="AspNetCoreDiagnosticObserverTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
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
using NUnit.Framework;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    public class AspNetCoreDiagnosticObserverTests : TracerInstanceTestsBase
    {
        [Test]
        public async Task CompleteDiagnosticObserverTest()
        {
            SetTracer(GetTracer());

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver() };
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
        }

        [Test]
        public void HttpRequestIn_PopulateSpan()
        {
            var tracer = GetTracer();

            IObserver<KeyValuePair<string, object>> observer = new AspNetCoreDiagnosticObserver(tracer, null);

            var context = new HostingApplication.Context { HttpContext = GetHttpContext() };

            observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context));

            var scope = tracer.ActiveScope;

            Assert.NotNull(scope);

            var span = scope.Span;

            Assert.NotNull(span);

            Assert.AreEqual("aspnet_core.request", span.OperationName);
            Assert.AreEqual("aspnet_core", span.GetTag(Tags.InstrumentationName));
            Assert.AreEqual(SpanTypes.Web, span.Type);
            Assert.AreEqual("GET /home/?/action", span.ResourceName);
            Assert.AreEqual(SpanKinds.Server, span.GetTag(Tags.SpanKind));
            Assert.AreEqual("GET", span.GetTag(Tags.HttpMethod));
            Assert.AreEqual("localhost", span.GetTag(Tags.HttpRequestHeadersHost));
            Assert.AreEqual("http://localhost/home/1/action", span.GetTag(Tags.HttpUrl));
            Assert.AreEqual(TracerConstants.Language, span.GetTag(Tags.Language));
        }

        private static Tracer GetTracer()
        {
            var settings = new TracerSettings();
            var writerMock = new Mock<IAgentWriter>();
            var samplerMock = new Mock<ISampler>();

            return new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
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
