// <copyright file="SelfInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Demos.Website_AspNetCore01
{
    public class SelfInvoker : IDisposable
    {
        private static readonly TimeSpan SleepDuration = TimeSpan.FromSeconds(1);

        private readonly CancellationToken _token;
        private readonly HttpClient _httpClient;

        public SelfInvoker(CancellationToken token)
        {
            _token = token;
            _httpClient = new HttpClient();
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task RunAsync(string rootUrl)
        {
            Console.WriteLine($"{this.GetType().Name} started.");

            while (!_token.IsCancellationRequested)
            {
                await Task.Delay(SleepDuration);
                await ExecuteIterationAsync(rootUrl);
            }

            Console.WriteLine($"{this.GetType().Name} stopped.");
        }

        private async Task ExecuteIterationAsync(string rootUrl)
        {
            try
            {
                var response = await _httpClient.GetAsync(rootUrl, _token);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Error] Request failed {response.StatusCode}");
                }
                else
                {
                    Console.WriteLine($"Request succeeded");
                }

                string responsePayload = await response.Content.ReadAsStringAsync();
                int responseLen = responsePayload.Length;
                Console.WriteLine($"Received response length: {responseLen}");
            }
            catch (TaskCanceledException)
            {
                // This is expected when the application ends
                // (no need to provide more exception details)
                Console.WriteLine($"SelfInvokerService: Current request '{rootUrl}' cancelled.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] {ex}");
            }
        }
    }
}