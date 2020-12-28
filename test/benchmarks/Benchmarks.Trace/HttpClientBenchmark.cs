using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.HttpClientHandler;
using Datadog.Trace.ClrProfiler.CallTarget;
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
        private static readonly CallTargetCustomHttpClientHandler CallTargetHandler = new CallTargetCustomHttpClientHandler();

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

            CallTargetHandler = new CallTargetCustomHttpClientHandler();
            CallTargetHandler.PublicSendAsync(HttpRequest, CancellationToken.None).GetAwaiter().GetResult();
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

        internal class CallTargetCustomHttpClientHandler : HttpClientHandler
        {
            private static readonly Task<HttpResponseMessage> CachedResult = Task.FromResult(new HttpResponseMessage());

            internal static HttpClientHandler Create() => new HttpClientHandler();

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return PublicSendAsync(request, cancellationToken);
            }

            public Task<HttpResponseMessage> PublicSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Task<HttpResponseMessage> result = CachedResult;
                CallTargetState state = CallTargetState.GetDefault();
                CallTargetReturn<Task<HttpResponseMessage>> cReturn = CallTargetReturn<Task<HttpResponseMessage>>.GetDefault();
                Exception exception = null;
                try
                {
                    try
                    {
                        state = CallTargetInvoker.BeginMethod<HttpClientHandlerIntegration, HttpClientHandler, HttpRequestMessage, CancellationToken>(this, request, cancellationToken);
                    }
                    catch(Exception ex)
                    {
                        CallTargetInvoker.LogException<HttpClientHandlerIntegration, HttpClientHandler>(ex);
                    }
                    result = CachedResult;
                }
                catch(Exception ex)
                {
                    exception = ex;
                    throw;
                }
                finally
                {
                    try
                    {
                        cReturn = CallTargetInvoker.EndMethod<HttpClientHandlerIntegration, HttpClientHandler, Task<HttpResponseMessage>>(this, result, exception, state);
                        result = cReturn.GetReturnValue();
                    }
                    catch (Exception ex)
                    {
                        CallTargetInvoker.LogException<HttpClientHandlerIntegration, HttpClientHandler>(ex);
                    }
                }
                return result;
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

        [Benchmark]
        public async Task<string> CallTargetSendAsync()
        {
            var task = CallTargetHandler.PublicSendAsync(HttpRequest, CancellationToken.None);
            await task;
            return "OK";
        }
    }
}
