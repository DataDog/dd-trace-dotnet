using System;
using System.Net.Http;
using Microsoft.Owin.Hosting;

namespace Samples.Owin.WebApi
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
                // Create HttpClient and make a request to api/values 
                HttpClient client = new HttpClient(); // Used to make sure we do automatic instrumentation
                Console.WriteLine("Press enter to close the webserver");
                Console.ReadLine();
            }
        }
    }
}
