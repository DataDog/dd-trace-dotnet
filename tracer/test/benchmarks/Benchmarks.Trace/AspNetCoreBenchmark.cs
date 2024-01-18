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
    [BenchmarkCategory(Constants.TracerCategory)]
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
            bench.SendRequest();
        }

        [Benchmark]
        public string SendRequest()
        {
            return Client.GetStringAsync("/Home").GetAwaiter().GetResult();
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
