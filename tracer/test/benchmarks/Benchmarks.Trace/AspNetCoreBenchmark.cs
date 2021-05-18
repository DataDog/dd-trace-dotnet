#if !NETFRAMEWORK

using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class AspNetCoreBenchmark
    {
        private static readonly HttpClient Client;

        static AspNetCoreBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            Client = testServer.CreateClient();

            Datadog.Trace.ClrProfiler.Instrumentation.Initialize();

            HomeController.Initialize();

            var bench = new AspNetCoreBenchmark();
            bench.SendRequest();
            bench.CallTargetSendRequest();
        }

        [Benchmark]
        public string SendRequest()
        {
            return Client.GetStringAsync("/Home").GetAwaiter().GetResult();
        }

        [Benchmark]
        public string CallTargetSendRequest()
        {
            return Client.GetStringAsync("/CallTargetHome").GetAwaiter().GetResult();
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
        private static readonly HttpMessageHandler Handler = new CustomHttpClientHandler();
        private static readonly object BoxedCancellationToken = new CancellationToken();
        private static int _mdToken;
        private static IntPtr _guidPtr;

        internal static void Initialize()
        {
            var methodInfo = typeof(HttpMessageHandler).GetMethod("SendAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            _mdToken = methodInfo.MetadataToken;
            var guid = typeof(HttpMessageHandler).Module.ModuleVersionId;

            _guidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, _guidPtr, false);
        }

        public async Task<string> Index()
        {
            var task = (Task)HttpMessageHandlerIntegration.HttpMessageHandler_SendAsync(
                Handler,
                HttpRequest,
                BoxedCancellationToken,
                (int)OpCodeValue.Callvirt,
                _mdToken,
                (long)_guidPtr);

            await task;

            return "OK";
        }

        internal class CustomHttpClientHandler : HttpClientHandler
        {
            private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

            internal static HttpClientHandler Create() => new HttpClientHandler();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return CachedResult;
            }
        }
    }

    /// <summary>
    /// Simple controller used for the calltarget aspnetcore benchmark
    /// </summary>
    public class CallTargetHomeController : Controller
    {
        private static readonly HttpRequestMessage HttpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };
        private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());
        
        public unsafe string Index()
        {
            CallTarget.Run<HttpClientHandlerIntegration, CallTargetHomeController, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
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
