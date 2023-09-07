using System;
using System.Net.Http;
using System.Threading.Tasks;
using Samples;

namespace AssemblyLoad.FileNotFoundException
{
    public class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                using var server = WebServer.Start(out var url);

                var regularHttpClient = new HttpClient { BaseAddress = new Uri(url) };

                await regularHttpClient.GetAsync("default-handler");

                Console.WriteLine("All is well!");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return (int)ExitCode.UnknownError;
            }

#if NETCOREAPP2_1
            // Add a delay to avoid a race condition on shutdown: https://github.com/dotnet/coreclr/pull/22712
            // This would cause a segmentation fault on .net core 2.x
            System.Threading.Thread.Sleep(5000);
#endif

            Console.WriteLine("App completed successfully");
            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
