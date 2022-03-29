using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;

namespace MismatchedTracerVersions.Cli
{
    public static class Program
    {
        public static async Task Main()
        {
            using var scope = Tracer.Instance.StartActive("main");

            // do this twice in case rejit hasn't happened yet
            for (var i = 0; i < 2; i++)
            {
                await MakeHttpRequest();
            }

            var assemblies = GetAssemblies();
            File.WriteAllLines(@"assemblies.txt", assemblies);

            // allow time for tracer to flush
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        private static async Task MakeHttpRequest()
        {
            HttpClient httpClient = new();

            try
            {
                // we're only insterested in instrumenting the method call,
                // so we don't care if it fails
                string timestamp = await httpClient.GetStringAsync("http://localhost/timestamp");
            }
            catch (Exception)
            {
            }

            await Task.Delay(200);
        }

        private static IEnumerable<string> GetAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                                          .Select(a => a.FullName)
                                          .Where(a => a.StartsWith("Datadog"))
                                          .Distinct()
                                          .OrderBy(a => a);
        }
    }
}
