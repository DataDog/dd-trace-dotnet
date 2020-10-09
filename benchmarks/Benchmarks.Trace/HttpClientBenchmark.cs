using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Integrations;
using Datadog.Trace.Configuration;

namespace Benchmarks.Trace
{
    [MemoryDiagnoser]
    public class HttpClientBenchmark
    {
        private static readonly HttpRequestMessage HttpRequest = new HttpRequestMessage { RequestUri = new Uri("http://datadoghq.com") };
        private static readonly HttpMessageHandler Handler = new CustomHttpClientHandler();
        private static readonly object BoxedCancellationToken = new CancellationToken();
        private static readonly int MdToken;
        private static readonly IntPtr GuidPtr;

        static HttpClientBenchmark()
        {
            var settings = new TracerSettings
            {
                StartupDiagnosticLogEnabled = false
            };

            Tracer.Instance = new Tracer(settings, new DummyAgentWriter(), null, null, null);

            var methodInfo = typeof(HttpMessageHandler).GetMethod("SendAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            MdToken = methodInfo.MetadataToken;
            var guid = typeof(HttpMessageHandler).Module.ModuleVersionId;

            GuidPtr = Marshal.AllocHGlobal(Marshal.SizeOf(guid));

            Marshal.StructureToPtr(guid, GuidPtr, false);

            new HttpClientBenchmark().SendAsync().GetAwaiter().GetResult();
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

        [Benchmark]
        public async Task<string> SendAsync()
        {
            var task = (Task)HttpMessageHandlerIntegration.HttpMessageHandler_SendAsync(
                Handler,
                HttpRequest,
                BoxedCancellationToken,
                (int)OpCodeValue.Callvirt,
                MdToken,
                (long)GuidPtr);

            await task;

            return "OK";
        }
    }
}
