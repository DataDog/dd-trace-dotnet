using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;

namespace WeatherService.NetFx461
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static async Task Main()
        {
            // The ServiceManifest.XML file defines one or more service type names.
            // Registering a service maps a service type name to a .NET type.
            // When Service Fabric creates an instance of this service type,
            // an instance of the class is created in this host process.
            await ServiceRuntime.RegisterServiceAsync("WeatherService_NetFx461", context => new WeatherService(context));

            // Prevents this host process from terminating so services keep running.
            await Task.Delay(Timeout.Infinite);
        }
    }
}
