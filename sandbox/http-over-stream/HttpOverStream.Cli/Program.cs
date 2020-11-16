using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace HttpOverStream.Cli
{
    public class Program
    {
        public static void Main()
        {
            var uri = new Uri("http://localhost:8126/v0.4/traces/");

            // start http server
            // using HttpListener listener = StartHttpListener(uri);
            // Thread.Sleep(TimeSpan.FromSeconds(1));

            // prepare the network stream
            NetworkStream stream = ConnectSocket(uri.DnsSafeHost, uri.Port);
            //var stream = new FileStream(@"C:\temp\http-request.bin", FileMode.Create, FileAccess.Write, FileShare.Read);

            // send request, get response
            var client = new HttpClient();
            HttpRequest request = CreateRequest(uri);
            HttpResponse response = client.Send(request, stream, stream);

            if (response == null)
            {
                Console.WriteLine("[client] Response == null");
                return;
            }

            Console.WriteLine($"[client] Response.StatusCode: {response.StatusCode}");
            Console.WriteLine($"[client] Response.ResponseMessage: {response.ResponseMessage}");

            foreach (var header in response.Headers)
            {
                Console.WriteLine($"[client] Response[{header.Name}]: {header.Value}");
            }

            if (response.ContentLength > 0 && response.Content is StreamContent streamContent)
            {
                Encoding encoding = response.GetContentEncoding();
                string responseContent;

                int length = (int)response.ContentLength;
                var responseContentBytes = new byte[length];
                int bytesRead = streamContent.Stream.Read(responseContentBytes, 0, length);
                responseContent = encoding.GetString(responseContentBytes, 0, bytesRead);

                /*
                using (var reader = new StreamReader(streamContent.Stream, encoding, detectEncodingFromByteOrderMarks: false, 2048))
                {
                    responseContent = reader.ReadToEnd();
                }
                */

                Console.WriteLine($"[client] {responseContent}");
            }

            Console.WriteLine();
        }

        private static HttpRequest CreateRequest(Uri uri)
        {
            /*
            X-Datadog-Trace-Count: 6
            Datadog-Meta-Lang-Interpreter: .NET Core
            Datadog-Meta-Lang-Version: 3.1.8
            Datadog-Meta-Lang: .NET
            Datadog-Meta-Tracer-Version: 1.19.6.0
            x-datadog-tracing-enabled: false
            Transfer-Encoding: chunked
            Content-Type: application/msgpack
            */

            var requestHeaders = new HttpHeaders()
            {
            };

            var content = new StringContent("Hello, world!", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new HttpRequest("POST", $"{uri.Host}:{uri.Port}", uri.PathAndQuery, requestHeaders, content);
        }

        private static NetworkStream ConnectSocket(string host, int port)
        {
            var ipAddress = Dns.GetHostAddresses(host).FirstOrDefault(t => t.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint endpoint = new IPEndPoint(ipAddress, port);

            Socket socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);

            return new NetworkStream(socket, FileAccess.ReadWrite, ownsSocket: true);
        }

        private static HttpListener StartHttpListener(Uri uri)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{uri.Host}:{uri.Port}{uri.AbsolutePath}");
            listener.Start();

            var thread = new Thread(AwaitRequests)
            {
                IsBackground = true,
                Name = "Http Listener Thread"
            };

            thread.Start(listener);
            return listener;
        }

        private static void AwaitRequests(object listener)
        {
            HttpListenerContext context = ((HttpListener)listener).GetContext();
            HttpListenerRequest request = context.Request;

            Console.WriteLine($"[server] {request.HttpMethod} {request.Url}");

            foreach (string header in request.Headers)
            {
                Console.WriteLine($"[server] Request[{header}]: {request.Headers[header]}");
            }

            var requestContentLength = (int)request.ContentLength64;
            var requestContentBytes = new byte[requestContentLength];
            _ = request.InputStream.Read(requestContentBytes, 0, requestContentLength);
            string requestContent = Encoding.UTF8.GetString(requestContentBytes);
            Console.WriteLine($"[server] {requestContent}");
            Console.WriteLine();

            var responseContentBytes = Encoding.UTF8.GetBytes("foo bar");

            HttpListenerResponse response = context.Response;
            response.StatusCode = 200;
            response.StatusDescription = "OK";
            response.ContentType = "text/plain";
            response.SendChunked = false;
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = responseContentBytes.Length;
            response.OutputStream.Write(responseContentBytes, 0, responseContentBytes.Length);
            response.OutputStream.Close();
            response.Close();
        }
    }
}
