using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Samples;

namespace HttpMessageHandler.StackOverflowException
{
    internal class Program
    {
        private static async Task<int> Main()
        {
            try
            {
                Console.WriteLine($"Profiler attached: {Samples.SampleHelpers.IsProfilerAttached()}");

                using var server = WebServer.Start(out var url);

                var baseAddress = new Uri(url);
                var regularHttpClient = new HttpClient { BaseAddress = baseAddress };
                var customHandlerHttpClient = new HttpClient(new DerivedHandler()) { BaseAddress = baseAddress };

                using (var scope = SampleHelpers.CreateScope("main"))
                {
                    Console.WriteLine("Calling regularHttpClient.GetAsync");
                    await regularHttpClient.GetAsync("default-handler");
                    Console.WriteLine("Called regularHttpClient.GetAsync");

                    Console.WriteLine("Calling customHandlerHttpClient.GetAsync");
                    await customHandlerHttpClient.GetAsync("derived-handler");
                    Console.WriteLine("Called customHandlerHttpClient.GetAsync");
                }

                Console.WriteLine("No stack overflow exceptions!");
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
    }

    public class DerivedHandler : HttpClientHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SampleHelpers.TrySetTag(SampleHelpers.GetActiveScope(), "class", nameof(DerivedHandler));

            Console.WriteLine("Calling base.SendAsync()");
            var result = await base.SendAsync(request, cancellationToken);
            Console.WriteLine("Called base.SendAsync()");
            return result;
        }
    }

    enum ExitCode : int
    {
        Success = 0,
        UnknownError = -10
    }
}
