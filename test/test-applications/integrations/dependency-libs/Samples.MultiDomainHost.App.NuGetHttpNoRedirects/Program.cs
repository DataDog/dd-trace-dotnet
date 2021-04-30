using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Samples.MultiDomainHost.App.NuGetHttpNoRedirects
{
    public class Program
    {
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            Console.WriteLine($"Executing {typeof(Program).FullName}.Main");
            InnerMethodToAllowProfilerInjection();
        }

        static void InnerMethodToAllowProfilerInjection()
        {
            // Add dependency on System.Net.WebClient which lives in the System assembly
            // System will always be loaded domain-neutral

            using (StartHttpListenerWithPortResilience("http://localhost:{0}/Samples.NuGetHttpNoRedirects/", out var url))
            {
                Console.WriteLine($"[WebClient] sending request to {url}");

                using (var webClient = new WebClient())
                {
                    webClient.Encoding = Encoding.UTF8;

                    var responseContent = webClient.DownloadString(url);
                }

                // Add dependency on System.Net.HttpMessageHandler which lives in the System.Net.Http assembly
                // System.Net.Http can be loaded in the named AppDomain if there's a bindingRedirect enforced
                Console.WriteLine($"[HttpClient] sending request to {url}");
                try
                {
                    var client = new HttpClient();
                    var response = client.GetAsync(url).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                }
                catch
                {
                    // do nothing
                }
            }

            Console.WriteLine("All done!");
        }

        private static HttpListener StartHttpListenerWithPortResilience(string uriTemplate, out string uri)
        {
            void HandleHttpRequests(object state)
            {
                var listener = (HttpListener)state;

                while (listener.IsListening)
                {
                    try
                    {
                        var context = listener.GetContext();

                        // write response content
                        byte[] responseBytes = Encoding.UTF8.GetBytes("OK");
                        context.Response.ContentEncoding = Encoding.UTF8;
                        context.Response.ContentLength64 = responseBytes.Length;
                        context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

                        // we must close the response
                        context.Response.Close();
                    }
                    catch (HttpListenerException)
                    {
                        // listener was stopped,
                        // ignore to let the loop end and the method return
                    }
                    catch (ObjectDisposedException)
                    {
                        // the response has been already disposed. 
                    }
                    catch (InvalidOperationException) when (!listener.IsListening)
                    {
                        // looks like it can happen on .NET Core when listener is stopped 
                    }
                }
            }

            int GetOpenPort()
            {
                TcpListener tcpListener = null;
                try
                {
                    tcpListener = new TcpListener(IPAddress.Loopback, 0);
                    tcpListener.Start();
                    return ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                }
                finally
                {
                    tcpListener?.Stop();
                }
            }

            string port = "9000";
            int retries = 5;

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                uri = string.Format(uriTemplate, port);

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(uri);

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
                    port = GetOpenPort().ToString();
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();
            }
        }

    }
}
