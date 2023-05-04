using Samples;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;

namespace Sandbox.AutomaticInstrumentation
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Set up local web server
            string port = args.FirstOrDefault(arg => arg.StartsWith("Port="))?.Split('=')[1] ?? "9000";
            using var server = WebServer.Start(port, out var url);
            server.RequestHandler = HandleHttpRequests;
            Console.WriteLine();
            Console.WriteLine($"Starting HTTP listener at {url}");
            Console.WriteLine();

            // Set the minimum permissions needed to run code in the new AppDomain
            PermissionSet permSet = new PermissionSet(PermissionState.None);
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution)); // REQUIRED to run code.
            permSet.AddPermission(new SecurityPermission(SecurityPermissionFlag.UnmanagedCode)); // REQUIRED for automatic instrumentation.
            permSet.AddPermission(new WebPermission(PermissionState.Unrestricted)); // REQUIRED for application to send traces to the Agent over HTTP. Also enables application to generate automatic instrumentation spans.

            var remote = AppDomain.CreateDomain("Remote", null, AppDomain.CurrentDomain.SetupInformation, permSet);

            try
            {
                remote.SetData("url", url);
                remote.DoCallBack(CreateAutomaticTraces);
                return (int)ExitCode.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"We have encountered an exception, the smoke test fails: {ex.Message}");
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }
            finally
            {
                AppDomain.Unload(remote);
            }
        }

        public static void CreateAutomaticTraces()
        {
            var url = AppDomain.CurrentDomain.GetData("url") as string;
            var iterations = 5;
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    // Create separate request objects since .NET Core asserts only one response per request
                    HttpWebRequest request = (HttpWebRequest)System.Net.WebRequest.Create(url);
                    request.GetResponse().Close();
                    Console.WriteLine($"Received {i + 1}/{iterations} response for request.GetResponse()");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to send request {i + 1}/{iterations}");
                    Console.WriteLine(ex);
                    throw;
                }
            }
        }

        private static void HandleHttpRequests(HttpListenerContext context)
        {
            Console.WriteLine("[HttpListener] received request");

            // read request content and headers
            using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
            {
                string requestContent = reader.ReadToEnd();
                Console.WriteLine($"[HttpListener] request content: {requestContent}");

                foreach (string headerName in context.Request.Headers)
                {
                    string headerValue = context.Request.Headers[headerName];
                    Console.WriteLine($"[HttpListener] request header: {headerName}={headerValue}");
                }
            }

            // write response content
            byte[] responseBytes = Encoding.UTF8.GetBytes("PONG");
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = responseBytes.Length;
            context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

            // we must close the response
            context.Response.Close();
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
