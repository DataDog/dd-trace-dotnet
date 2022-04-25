using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp
{
    internal class Program
    {
        private static async Task<int> Main()
        {
            var baseAddress = new Uri("https://www.example.com/");
            var regularHttpClient = new HttpClient { BaseAddress = baseAddress };

            Console.WriteLine("Calling regularHttpClient.GetAsync");
            await regularHttpClient.GetAsync("default-handler");
            Console.WriteLine("Called regularHttpClient.GetAsync");

            return 0;
        }
    }
}
