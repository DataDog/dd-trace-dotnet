#if !NETFRAMEWORK

using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
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
            Environment.SetEnvironmentVariable(ConfigurationKeys.TraceEnabled, "0");
            Environment.SetEnvironmentVariable(ConfigurationKeys.StartupDiagnosticLogEnabled, "0");

            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false,
                DiagnosticSourceEnabled = true
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var builder = new WebHostBuilder()
                .UseStartup<Startup>();

            var testServer = new TestServer(builder);
            Client = testServer.CreateClient();

            Datadog.Trace.ClrProfiler.Instrumentation.Initialize();

            HomeController.Initialize();
        }

        [Benchmark]
        public Task<string> SendRequest()
        {
            return Client.GetStringAsync("/Home");
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
    /// Simple controller used for the aspnetcore benchmark
    /// </summary>
    public class HomeController : Controller
    {
        private static readonly HttpRequestMessage HttpRequest = new HttpRequestMessage();
        private static readonly HttpMessageHandler Handler = new CustomHttpMessageHandler();
        private static readonly object BoxedCancellationToken = new CancellationToken();
        private static int _mdToken;
        private static IntPtr _guidPtr;

        internal static void Initialize()
        {
            HttpMessageHandlerIntegration.HttpClientHandler = typeof(CustomHttpMessageHandler).FullName;

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
                111,
                _mdToken,
                (long)_guidPtr);

            await task;

            return "OK";
        }

        internal class CustomHttpMessageHandler : HttpMessageHandler
        {
            private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

            internal static HttpClientHandler Create() => new HttpClientHandler();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return CachedResult;
            }
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
        public Task<string> SendRequest()
        {
            return null;
        }
    }
}

#endif
