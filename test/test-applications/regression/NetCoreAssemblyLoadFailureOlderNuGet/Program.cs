using System;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace;

namespace NetCoreAssemblyLoadFailureOlderNuGet
{
    public class Program
    {
        static async Task<int> Main()
        {
            try
            {
                var url = GetUrl();
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

            return (int)ExitCode.Success;
        }

        private static string GetUrl()
        {
            return "http://www.example.com";
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
