// <copyright file="SelfInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BuggyBits
{
    public class SelfInvoker : IDisposable
    {
        private static readonly TimeSpan SleepDuration = TimeSpan.FromMilliseconds(100);

        private readonly CancellationToken _exitToken;
        private readonly HttpClient _httpClient;
        private readonly Scenario _scenario;

        public SelfInvoker(CancellationToken token, Scenario scenario)
        {
            _exitToken = token;
            _httpClient = new HttpClient();
            _scenario = scenario;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task RunAsync(string rootUrl, int iterations = 0)
        {
            Console.WriteLine($"{this.GetType().Name} started.");

            try
            {
                string asyncEndpoint = GetEndpoint(rootUrl);

                // Run for the given number of iterations
                // 0 means wait for cancellation
                int current = 0;
                while (
                    ((iterations == 0) && !_exitToken.IsCancellationRequested) ||
                    (iterations > current))
                {
                    await Task.Delay(SleepDuration);
                    await ExecuteIterationAsync(asyncEndpoint);

                    current++;
                }
            }
            catch (Exception x)
            {
                Console.WriteLine($"{x.GetType().Name} | {x.Message}");
            }

            Console.WriteLine($"{this.GetType().Name} stopped.");
        }

        private string GetEndpoint(string rootUrl)
        {
            switch (_scenario)
            {
                case Scenario.StringConcat:
                default:
                    return $"{rootUrl}/Products/Index";

                case Scenario.StringBuilder:
                    return $"{rootUrl}/Products/Builder";

                case Scenario.Parallel:
                    return $"{rootUrl}/Products/Parallel";

                case Scenario.Async:
                    return $"{rootUrl}/Products/async";
            }
        }

        private async Task ExecuteIterationAsync(string endpointUrl)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(endpointUrl, _exitToken);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Error] Request failed {response.StatusCode}");
                    return;
                }

                string responsePayload = await response.Content.ReadAsStringAsync();
                int responseLen = responsePayload.Length;
                sw.Stop();
                Console.WriteLine($"{endpointUrl} | response length = {responseLen} in {sw.ElapsedMilliseconds} ms");
            }
            catch (TaskCanceledException)
            {
                // Excepted when the application exits
                Console.WriteLine($"SelfInvokerService: {endpointUrl} request cancelled.");
            }
        }
    }
}