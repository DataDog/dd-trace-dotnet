using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Samples.NetFramework.DomainNeutralInstrumentationWithoutGac
{
    public static class Program
    {
        /// <summary>
        /// Instruct the runtime to load assemblies as domain-neutral, if possible.
        /// Although there are async methods throughout the application, setting the top-level
        /// Main method as 'async Task' instead of 'void' messes with the domain-neutral loading,
        /// so leave as 'void'.
        /// </summary>
        /// <param name="args"></param>
        [LoaderOptimization(LoaderOptimization.MultiDomainHost)]
        static void Main(string[] args)
        {
            InnerMethodToAllowProfilerInjection().GetAwaiter().GetResult();
        }

        static async Task InnerMethodToAllowProfilerInjection()
        {
            var url = "http://www.contoso.com/";
            var additionalDelay = 2000;

            // Add dependency on System.Net.WebClient which lives in the System assembly
            // System will always be loaded domain-neutral
            Console.WriteLine($"[WebClient] sending request to {url}");
            using (var webClient = new WebClient())
            {
                webClient.Encoding = Encoding.UTF8;
                webClient.DownloadString(url);
            }

            // Add dependency on System.Net.HttpMessageHandler which lives in the System.Net.Http assembly
            // System.Net.Http will always be loaded domain-neutral since we're not adding it as a NuGet package reference
            Console.WriteLine($"[HttpClient] sending request to {url}");
            var client = new HttpClient();
            await client.GetAsync(url);

            Console.WriteLine($"Waiting {additionalDelay} ms to allow Tracer to flush");
            await Task.Delay(additionalDelay);
            Console.WriteLine("All done!");
        }
    }
}
