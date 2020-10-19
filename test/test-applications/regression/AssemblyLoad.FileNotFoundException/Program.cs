using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace AssemblyLoad.FileNotFoundException
{
    public class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                var baseAddress = new Uri("https://www.example.com/");
                var regularHttpClient = new HttpClient { BaseAddress = baseAddress };

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

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
