using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;
using Samples;

namespace NetCoreAssemblyLoadFailureOlderNuGet
{
    public class Program
    {
        static async Task<int> Main()
        {
            try
            {
                using var server = WebServer.Start(out var url);

                await RunProgramAsync(url);
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

        private static async Task RunProgramAsync(string url)
        {
            using (Tracer.Instance.StartActive("RunProgramAsync"))
            using (var client = new HttpClient())
            using (Tracer.Instance.StartActive("GetAsync"))
            {
                await client.GetAsync(url);
            }
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
