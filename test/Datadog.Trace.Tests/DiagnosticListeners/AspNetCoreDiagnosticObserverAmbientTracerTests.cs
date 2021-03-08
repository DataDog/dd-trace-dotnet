#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Sampling;
using Microsoft.AspNetCore.Hosting;
#if NETCOREAPP2_1
using Microsoft.AspNetCore.Hosting.Internal;
#endif
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Moq;
using Xunit;
using Xunit.Sdk;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    [Collection(nameof(TracerInstanceTestCollection))]
    [TracerRestorer]
    public class AspNetCoreDiagnosticObserverAmbientTracerTests
    {
        [Fact]
        public async Task<string> CompleteDiagnosticObserverTest()
        {
            Tracer.Instance = GetTracer();

            var builder = new WebHostBuilder()
                .UseStartup<MvcStartup>();

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

            return retValue;
        }

        [Fact]
        public void HttpRequestIn_PopulateSpan()
        {
            var tracer = GetTracer();

            IObserver<KeyValuePair<string, object>> observer = new AspNetCoreDiagnosticObserver(tracer);

#if NETCOREAPP2_1
            var context = new HostingApplication.Context { HttpContext = GetHttpContext() };
#else
            var context = new { HttpContext = GetHttpContext() };
#endif

            observer.OnNext(new KeyValuePair<string, object>("Microsoft.AspNetCore.Hosting.HttpRequestIn.Start", context));

            var scope = tracer.ActiveScope;

            Assert.NotNull(scope);

            var span = scope.Span;

            Assert.NotNull(span);

            Assert.Equal("aspnet_core.request", span.OperationName);
            Assert.Equal("aspnet_core", span.GetTag(Tags.InstrumentationName));
            Assert.Equal(SpanTypes.Web, span.Type);
            Assert.Equal("GET /home/?/action", span.ResourceName);
            Assert.Equal(SpanKinds.Server, span.GetTag(Tags.SpanKind));
            Assert.Equal("GET", span.GetTag(Tags.HttpMethod));
            Assert.Equal("localhost", span.GetTag(Tags.HttpRequestHeadersHost));
            Assert.Equal("http://localhost/home/1/action", span.GetTag(Tags.HttpUrl));
            Assert.Equal(TracerConstants.Language, span.GetTag(Tags.Language));
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

        [AttributeUsage(AttributeTargets.Class, Inherited = true)]
        private class TracerRestorerAttribute : BeforeAfterTestAttribute
        {
            private Tracer _tracer;

            public override void Before(MethodInfo methodUnderTest)
            {
                _tracer = Tracer.Instance;
                base.Before(methodUnderTest);
            }

            public override void After(MethodInfo methodUnderTest)
            {
                Tracer.Instance = _tracer;
                base.After(methodUnderTest);
            }
        }
    }
}

#endif
