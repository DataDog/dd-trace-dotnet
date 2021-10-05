using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;
using Datadog.Trace.TestHelpers;

namespace HttpMessageHandler.StackOverflowException
{
    internal class Program
    {
        private static string _url;

        private static async Task<int> Main()
        {
            try
            {
                Console.WriteLine($"Profiler attached: {Samples.SampleHelpers.IsProfilerAttached()}");

                using (StartHttpListenerWithPortResilience())
                {
                    var baseAddress = new Uri(_url);
                    var regularHttpClient = new HttpClient { BaseAddress = baseAddress };
                    var customHandlerHttpClient = new HttpClient(new DerivedHandler()) { BaseAddress = baseAddress };

                    using (var scope = Tracer.Instance.StartActive("main"))
                    {
                        Console.WriteLine("Calling regularHttpClient.GetAsync");
                        await regularHttpClient.GetAsync("default-handler");
                        Console.WriteLine("Called regularHttpClient.GetAsync");

                        Console.WriteLine("Calling customHandlerHttpClient.GetAsync");
                        await customHandlerHttpClient.GetAsync("derived-handler");
                        Console.WriteLine("Called customHandlerHttpClient.GetAsync");
                    }

                    Console.WriteLine("No stack overflow exceptions!");
                    Console.WriteLine("All is well!");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif

            return (int)ExitCode.Success;
        }

        public static HttpListener StartHttpListenerWithPortResilience(int retries = 5)
        {
            var port = TcpPortProvider.GetOpenPort().ToString();

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                _url = $"http://localhost:{port}/StackOverflowException/";

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(_url);

                try
                {
                    listener.Start();

                    var listenerThread = new Thread(HandleHttpRequests) { IsBackground = true };
                    listenerThread.Start(listener);

                    return listener;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    port = TcpPortProvider.GetOpenPort().ToString();
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

        private static void HandleHttpRequests(object state)
        {
            var listener = (HttpListener)state;

            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();

                    var responseBytes = Encoding.UTF8.GetBytes("OK");
                    context.Response.ContentEncoding = Encoding.UTF8;
                    context.Response.ContentLength64 = responseBytes.Length;
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

                    context.Response.Close();
                }
                catch (HttpListenerException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }

    public class DerivedHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Tracer.Instance.ActiveScope?.Span.SetTag("class", nameof(DerivedHandler));

            Console.WriteLine("Calling base.SendAsync()");
            var result = await base.SendAsync(request, cancellationToken);
            Console.WriteLine("Called base.SendAsync()");
            return result;
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
