using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Samples.OpenTelemetry.WebRequest
{
    public static class Program
    {
        private static readonly Encoding Utf8 = Encoding.UTF8;

        public static async Task Main(string[] args)
        {
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            Console.WriteLine($"Port {port}");

            using (var server = WebServer.Start(port, out var url))
            {
                server.RequestHandler = HandleHttpRequests;

                Console.WriteLine();
                Console.WriteLine($"Starting HTTP listener at {url}");

                await RequestHelpers.SendRequestsAsync(url);

                Console.WriteLine();
                Console.WriteLine("Stopping HTTP listener.");
            }

            // Force process to end, otherwise the background listener thread lives forever in .NET Core.
            Environment.Exit(0);
        }

        private static void HandleHttpRequests(HttpListenerContext context)
        {
            Console.WriteLine($"[HttpListener] received request: {context.Request.HttpMethod} {context.Request.RawUrl}");

            var payload = Utf8.GetBytes("PONG");
            context.Response.ContentEncoding = Utf8;

            var path = context.Request.Url?.AbsolutePath;

            if (path != null && path.EndsWith("/redirect"))
            {
                context.Response.StatusCode = 302;
                var redirectTarget = context.Request.Url.GetLeftPart(UriPartial.Authority) + path.Substring(0, path.Length - "redirect".Length) + "ok";
                context.Response.RedirectLocation = redirectTarget;
            }
            else if (path != null && path.EndsWith("/client-error"))
            {
                context.Response.StatusCode = 400;
            }
            else if (path != null && path.EndsWith("/server-error"))
            {
                context.Response.StatusCode = 500;
            }
            else
            {
                context.Response.StatusCode = 200;
            }

            context.Response.ContentLength64 = payload.Length;
            context.Response.OutputStream.Write(payload, 0, payload.Length);
            context.Response.Close();
        }
    }
}
