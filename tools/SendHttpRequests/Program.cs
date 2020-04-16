using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;

namespace SendHttpRequests
{
    public class Program
    {
        [Argument(0, "urls", "List of target URLs")]
        [Required(AllowEmptyStrings = false)]
        public string[] Urls { get; }

        [Option("--rps", "Requests per second", CommandOptionType.SingleValue)]
        [Required]
        public int RequestsPerSecond { get; }

        public static Task<int> Main(string[] args)
            => CommandLineApplication.ExecuteAsync<Program>(args);

        private Task OnExecuteAsync()
        {
            if (Urls == null || Urls.Length == 0)
            {
                // this should never happen since CommandLineApplication validates inputs
                Console.WriteLine("No urls specified.");
                return Task.CompletedTask;
            }

            Console.WriteLine("Press CTRL+C to exit...");
            var tasks = new List<Task>(Urls.Length);

            foreach (string url in Urls)
            {
                Console.WriteLine($"Sending {RequestsPerSecond} requests per second to {url}.");

                var requestGenerator = new HttpRequestGenerator(url);
                Task task = requestGenerator.StartAsync(RequestsPerSecond);
                tasks.Add(task);
            }

            return Task.WhenAll(tasks);
        }

        public class HttpRequestGenerator
        {
            private readonly Stopwatch _stopwatch = new Stopwatch();

            private readonly string _url;

            private readonly HttpClient _httpClient = new HttpClient();

            public HttpRequestGenerator(string url)
            {
                _url = url;
                _stopwatch.Start();
            }

            public async Task StartAsync(int requestsPerSecond)
            {
                // give server time to boot up
                await Task.Delay(TimeSpan.FromSeconds(5));

                // send first request to warm up the server
                (await _httpClient.GetAsync(_url)).EnsureSuccessStatusCode();

                // seconds between each request
                var period = TimeSpan.FromSeconds(1.0 / requestsPerSecond);

                var stopwatch = new Stopwatch();

                while (true)
                {
                    stopwatch.Restart();

                    try
                    {
                        (await _httpClient.GetAsync(_url)).EnsureSuccessStatusCode();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Environment.Exit(1);
                    }

                    // adjust the delay to get closer to the requested RPS
                    TimeSpan timeLeft = period - stopwatch.Elapsed;

                    if (timeLeft > TimeSpan.Zero)
                    {
                        await Task.Delay(timeLeft);
                    }
                }
            }
        }
    }
}
