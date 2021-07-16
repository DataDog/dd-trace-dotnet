using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;

namespace DuplicateTypeProxy
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var listener = StartHttpListenerWithPortResilience(out var uri);

            await RunAsync(typeof(HttpClient), uri);

            for (int i = 0; i < 5; i++)
            {
                var assembly = Assembly.LoadFile(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"), uri);
            }

#if NETCOREAPP3_1 || NET5_0
            for (int i = 0; i < 5; i++)
            {
                var alc = new System.Runtime.Loader.AssemblyLoadContext($"Context: {i}");
                var assembly = alc.LoadFromAssemblyPath(typeof(HttpClient).Assembly.Location);
                await RunAsync(assembly.GetType("System.Net.Http.HttpClient"), uri);
            }
#endif
        }

        public static async Task RunAsync(Type httpClientType, string uri)
        {
            var instance = Activator.CreateInstance(httpClientType);
            var getAsync = httpClientType.GetMethod("GetAsync", new[] { typeof(string) });

            var task = (Task)getAsync.Invoke(instance, new[] { uri });
            await task;
        }

        private static HttpListener StartHttpListenerWithPortResilience(out string uri)
        {
            static async Task HandleHttpRequests(object state)
            {
                var listener = (HttpListener)state;

                var payload = Encoding.UTF8.GetBytes("OK");

                while (listener.IsListening)
                {
                    try
                    {
                        var context = await listener.GetContextAsync();

                        context.Response.ContentEncoding = Encoding.UTF8;
                        context.Response.ContentLength64 = payload.Length;
                        context.Response.OutputStream.Write(payload, 0, payload.Length);
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // listener was stopped,
                        // ignore to let the loop end and the method return
                    }
                }
            }

            int retries = 5;

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                var port = TcpPortProvider.GetOpenPort().ToString();

                uri = $"http://localhost:{port}/Samples.DuplicateTypeProxy/";

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(uri);

                try
                {
                    listener.Start();

                    _ = Task.Run(() => HandleHttpRequests(listener));

                    return listener;
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }
    }
}
