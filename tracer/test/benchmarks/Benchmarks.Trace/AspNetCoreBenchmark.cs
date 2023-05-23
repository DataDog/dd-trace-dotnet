#if !NETFRAMEWORK

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    [BenchmarkAgent1]
    public class AspNetCoreBenchmark
    {
        private static readonly HttpClient Client;

        static AspNetCoreBenchmark()
        {
            var settings = new TracerSettings { StartupDiagnosticLogEnabled = false };

            Tracer.UnsafeSetTracerInstance(new Tracer(settings, new DummyAgentWriter(), null, null, null));

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            Client = testServer.CreateClient();

            Datadog.Trace.ClrProfiler.Instrumentation.Initialize();

            var bench = new AspNetCoreBenchmark();
            bench.SendRequest().GetAwaiter().GetResult();
        }

        [Benchmark]
        public async Task<string> SendRequest()
        {
            return await Client.GetStringAsync("/Home");
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
        private static readonly HttpRequestMessage HttpRequest = new() { RequestUri = new Uri("http://datadoghq.com") };
        private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

        public async Task<string> Index()
        {
            await CallTargetRun();
            return "OK";

            unsafe Task<HttpResponseMessage> CallTargetRun()
                => CallTarget.Run<HttpClientHandlerIntegration, HomeController, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>(this, HttpRequest, CancellationToken.None, &GetResult);
            
            static Task<HttpResponseMessage> GetResult(HttpRequestMessage request, CancellationToken cancellationToken)
                => CachedResult;
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
