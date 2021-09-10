using System;
using System.Net.Http;
using System.Threading;
using Microsoft.Owin.Hosting;

namespace Samples.Owin.WebApi2
{
    public class Program
    {
        private static string Host()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:9000/";
        }

        static void Main()
        {
            // Start OWIN host 
            var host = Host();
            Console.WriteLine($"Starting Owin app listening on {host}");
            using (WebApp.Start<Startup>(url: host))
            {
                Console.WriteLine("Webserver started");
                // Console.ReadLine doesn't work apparently.
                var stopHandle = new ManualResetEvent(false);
                stopHandle.WaitOne();

            }
            Console.WriteLine("Shutting down server");
        }
    }
}
