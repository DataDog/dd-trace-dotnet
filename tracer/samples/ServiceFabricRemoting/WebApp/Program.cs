using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.ServiceFabric.Services.Runtime;

namespace WebApp
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main(string[] args)
        {
            if (args?.Length > 0 && args.Contains("standalone", StringComparer.OrdinalIgnoreCase))
            {
                await CreateHostBuilder(args).Build().RunAsync();
            }
            else
            {
                // The ServiceManifest.XML file defines one or more service type names.
                // Registering a service maps a service type name to a .NET type.
                // When Service Fabric creates an instance of this service type,
                // an instance of the class is created in this host process.
                await ServiceRuntime.RegisterServiceAsync("WebApp", context => new WebApp(context));

                // Prevents this host process from terminating so services keeps running.
                await Task.Delay(Timeout.Infinite);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                                          {
                                              webBuilder.UseStartup<Startup>();
                                          });
    }
}
