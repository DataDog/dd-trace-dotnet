using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace Samples.MultiDomainHost.App.NuGetJsonWithRedirects
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
            // Add static dependency to Newtonsoft.Json
            var serializer = new JsonSerializer();

            var url = "http://www.contoso.com/";

            // Add dependency on System.Net.WebClient which lives in the System assembly
            // System will always be loaded domain-neutral
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

            Console.WriteLine("All done!");
        }
    }
}
