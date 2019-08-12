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

            return (int)ExitCode.Success;
        }

        enum ExitCode : int
        {
            Success = 0,
            UnknownError = -10
        }
    }
}
