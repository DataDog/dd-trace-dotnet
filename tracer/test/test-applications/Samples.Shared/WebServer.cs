using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Samples
{
    public class WebServer : IDisposable
    {
        private readonly HttpListener _listener;

        private WebServer(HttpListener listener)
        {
            _listener = listener;
            _ = Task.Run(() => HandleHttpRequests(listener));
        }

        public Action<HttpListenerContext> RequestHandler { get; set; }

        public static WebServer Start(string port, out string uri)
        {
            if (port == null)
            {
                port = GetOpenPort().ToString();
            }

            int retries = 5;

            // try up to 5 consecutive ports before giving up
            while (true)
            {
                uri = $"http://localhost:{port}/{Guid.NewGuid()}/";

                // seems like we can't reuse a listener if it fails to start,
                // so create a new listener each time we retry
                var listener = new HttpListener();
                listener.Prefixes.Add(uri);

                try
                {
                    listener.Start();

                    return new WebServer(listener);
                }
                catch (HttpListenerException) when (retries > 0)
                {
                    // only catch the exception if there are retries left
                    retries--;
                }

                // always close listener if exception is thrown,
                // whether it was caught or not
                listener.Close();

                port = GetOpenPort().ToString();
            }
        }

        public static WebServer Start(out string uri)
        {
            return Start(null, out uri);
        }

        public void Dispose()
        {
            _listener.Close();
        }

        private static void DefaultRequestHandler(HttpListenerContext context)
        {
            var payload = Encoding.UTF8.GetBytes("OK");

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = payload.Length;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
            context.Response.Close();
        }

        private static int GetOpenPort()
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

        private async Task HandleHttpRequests(object state)
        {
            var listener = (HttpListener)state;

            while (listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();

                    (RequestHandler ?? DefaultRequestHandler).Invoke(context);
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
                catch (InvalidOperationException)
                {
                    // this can occur when setting Response.ContentLength64, with the framework claiming that the response has already been submitted
                    // for now ignore, and we'll see if this introduces downstream issues
                }
                catch (Exception) when (!listener.IsListening)
                {
                    // we don't care about any exception when listener is stopped
                }
            }
        }
    }
}
