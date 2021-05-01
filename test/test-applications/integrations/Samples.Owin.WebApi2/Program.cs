using System;
using System.Net.Http;
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
            using (WebApp.Start<Startup>(url: Host()))
            {
                Console.WriteLine("Press enter to close the webserver");
                Console.ReadLine();
            }
        }
    }
}
