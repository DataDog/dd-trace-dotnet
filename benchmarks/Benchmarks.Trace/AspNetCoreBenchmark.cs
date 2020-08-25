#if !NETFRAMEWORK
extern alias Http; // Import the "real" HttpClient

// ReSharper disable RedundantNameQualifier -- We're doing some namespace trickeries here
// Resharper is going to tell you the fully qualified namespaces are not necessary, that's a lie

using System;
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
        private static readonly System.Net.Http.HttpClient Client;

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

    public class HomeController : Controller
    {
        private static System.Net.Http.HttpRequestMessage _request = new System.Net.Http.HttpRequestMessage();
        private static System.Net.Http.HttpClientHandler _handler = System.Net.Http.HttpClientHandler.Create();
        private static object _boxedCancellationToken = new CancellationToken();
        private static int _mdToken;
        private static IntPtr _guidPtr;

        internal static void Initialize()
        {
            var methodInfo = typeof(Http::System.Net.Http.HttpMessageHandler).GetMethod("SendAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            _mdToken = methodInfo.MetadataToken;
            var guid = typeof(Http::System.Net.Http.HttpMessageHandler).Module.ModuleVersionId;

            _guidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, _guidPtr, false);
        }

        public async Task<string> Index()
        {
            var task = (Task)HttpMessageHandlerIntegration.HttpMessageHandler_SendAsync(
                _handler,
                _request,
                _boxedCancellationToken,
                111,
                _mdToken,
                (long)_guidPtr);

            await task;

            return "OK";
        }
    }
}

// The HttpMessageHandler instrumentation expects an instance of System.Net.Http.HttpClientHandler
// To avoid dependencies on an actual HTTP stack, declare our own type with the same name
namespace System.Net.Http
{
    internal class HttpClientHandler : Http::System.Net.Http.HttpMessageHandler
    {
        private static readonly Task<Http::System.Net.Http.HttpResponseMessage> CachedResult = Task.FromResult(new Http::System.Net.Http.HttpResponseMessage());

        internal static HttpClientHandler Create() => new HttpClientHandler();

        protected override Task<Http::System.Net.Http.HttpResponseMessage> SendAsync(Http::System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return CachedResult;
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
