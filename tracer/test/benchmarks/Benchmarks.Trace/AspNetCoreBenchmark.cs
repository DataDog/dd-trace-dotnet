#if !NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.SpanCodeOrigin;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Iast.Settings;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Iast;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkCategory(Constants.TracerCategory, Constants.RunOnPrs, Constants.RunOnMaster)]
    public class AspNetCoreBenchmark
    {
        private HttpClient _client;
        private Tracer _tracer;
        private Security _security;
        private Datadog.Trace.Iast.Iast _iast;
        private SpanCodeOrigin _spanCodeOrigin;
        private DiagnosticManager _diagnosticManager;
        private TestServer _testServer;

        [GlobalSetup]
        public void GlobalSetup()
        {
            var config = new CustomSettingsForTests(TracerHelper.DefaultConfig);
            var settings = new TracerSettings(config, NullConfigurationTelemetry.Instance, new());

            _tracer = TracerHelper.CreateTracer(settings);
            _security = new Security(new SecuritySettings(config, NullConfigurationTelemetry.Instance), null, new RcmSubscriptionManager());
            _iast = new Datadog.Trace.Iast.Iast(new IastSettings(config, NullConfigurationTelemetry.Instance), NullDiscoveryService.Instance);

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            _testServer = new TestServer(builder);
            _client = _testServer.CreateClient();

            var observers = new List<DiagnosticObserver>();
            _spanCodeOrigin = new SpanCodeOrigin(new DebuggerSettings(config, NullConfigurationTelemetry.Instance));
            observers.Add(new AspNetCoreDiagnosticObserver(_tracer, _security, _iast, _spanCodeOrigin));
            _diagnosticManager = new DiagnosticManager(observers);
            _diagnosticManager.Start();

            // Warmup to initialize middleware pipeline
            SendRequest();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _diagnosticManager.Dispose();
            _testServer.Dispose();
            _security.Dispose();
            _tracer.TracerManager.ShutdownAsync().GetAwaiter().GetResult();
        }

        [Benchmark]
        public string SendRequest()
        {
            return _client.GetStringAsync("/Home").GetAwaiter().GetResult();
        }

        private class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvc();
            }

            public void Configure(IApplicationBuilder builder)
            {
                builder.UseRouting();
                builder.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllerRoute(
                        name: "default",
                        pattern: "{controller=Home}/{action=Index}/{id?}");
                });
            }
        }
    }

    /// <summary>
    /// Simple controller used for the aspnetcore benchmark
    /// </summary>
    public class HomeController : Controller
    {
        private static readonly HttpRequestMessage HttpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };
        private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

        public unsafe string Index()
        {
            CallTarget.Run<HttpClientHandlerIntegration, HomeController, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
                (this, HttpRequest, CancellationToken.None, &GetResult).GetAwaiter().GetResult();

            return "OK";

            static Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken) => CachedResult;
        }
    }
}
#else

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class AspNetCoreBenchmark
    {
        [GlobalSetup]
        public void GlobalSetup()
        {
        }

        [Benchmark]
        public string SendRequest()
        {
            return null;
        }

        [Benchmark]
        public string CallTargetSendRequest()
        {
            return null;
        }
    }
}

#endif
