using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Samples.FakeKudu
{
    internal static class Program
    {
        private static async Task Main()
        {
            // Make some request that would be traced if we weren't the SCM process
            var client = new HttpClient();
            // Probably a 404
            var response = await client.GetAsync("http://localhost:8126");
            Console.WriteLine(response.ToString());
        }
    }
}
