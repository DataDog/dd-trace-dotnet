using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.AzureFunctions.Version3.OutOfProcess
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // #if DEBUG
            //             Debugger.Launch();
            // #endif
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(s =>
                {
                    //s.AddHttpClient();
                    //s.AddLogging();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
