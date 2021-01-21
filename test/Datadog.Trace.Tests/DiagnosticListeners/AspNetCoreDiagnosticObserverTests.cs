#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Sampling;
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
    [CollectionDefinition(nameof(AspNetCoreDiagnosticObserverTests), DisableParallelization = true)]
    [TracerRestorer]
    public class AspNetCoreDiagnosticObserverTests
    {
        [Fact]
        public async Task<string> CompleteDiagnosticObserverTest()
        {
            Tracer.Instance = GetTracer();

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

            return retValue;
        }

        [Fact]
        public void HttpRequestIn_PopulateSpan()
        {
            var tracer = GetTracer();

            IObserver<KeyValuePair<string, object>> observer = new AspNetCoreDiagnosticObserver(tracer);

            var context = new HostingApplication.Context { HttpContext = GetHttpContext() };

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

        [Theory]
        [InlineData("/", "GET /Home/Index")]
        [InlineData("/home", "GET /Home/Index")]
        [InlineData("/home/get", "GET /Home/Get")]
        [InlineData("/home/get/3", "GET /Home/Get")]
        [InlineData("/home/error", "GET /Home/Error")]
        [InlineData("/Api", "GET /api/{id?}")]
        [InlineData("/api/123", "GET /api/{id?}")]
        [InlineData("/unknown", "GET /unknown")]
        [InlineData("/home/1/action", "GET /home/?/action")]
        public async Task SpanResourceName_IsConsistentAcrossRoutes(string path, string expectedSpanResourceName)
        {
            var writer = new LoggingAgentWriter();
            Tracer.Instance = GetTracer(writer);

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            var client = testServer.CreateClient();
            var observers = new List<DiagnosticObserver> { new AspNetCoreDiagnosticObserver() };

            using (var diagnosticManager = new DiagnosticManager(observers))
            {
                diagnosticManager.Start();
                DiagnosticManager.Instance = diagnosticManager;
                try
                {
                    await client.GetStringAsync(path);
                }
                catch { }

                Assert.Contains(writer.Spans, x => x.ResourceName == expectedSpanResourceName);
                DiagnosticManager.Instance = null;
            }
        }

        private static Tracer GetTracer(IAgentWriter writer = null)
        {
            var settings = new TracerSettings();
            writer ??= new Mock<IAgentWriter>().Object;
            var samplerMock = new Mock<ISampler>();

            return new Tracer(settings, writer, samplerMock.Object, scopeManager: null, statsd: null);
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

        private class LoggingAgentWriter : IAgentWriter
        {
            public List<Span> Spans { get; } = new List<Span>();

            public Task FlushAndCloseAsync() => Task.CompletedTask;

            public Task FlushTracesAsync() => Task.CompletedTask;

            public Task<bool> Ping() => Task.FromResult(true);

            public void WriteTrace(Span[] trace) => Spans.AddRange(trace);
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

        public string Get(int id) => $"Hello {id}";

        public void Error()
        {
            throw new Exception();
        }
    }

    public class ApiController
    {
        [HttpGet("api/{id?}")]
        public string Get() => "Hello world";
    }
}

#endif
