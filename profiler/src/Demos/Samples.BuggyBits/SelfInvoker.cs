// <copyright file="SelfInvoker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
        private readonly int _nbIdleThreads;

        public SelfInvoker(CancellationToken token, Scenario scenario, int nbIdleThreds)
        {
            _exitToken = token;
            _httpClient = new HttpClient();
            _scenario = scenario;
            _nbIdleThreads = nbIdleThreds;
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }

        public async Task RunAsync(string rootUrl, int iterations = 0)
        {
            Console.WriteLine($"{this.GetType().Name} started.");

            CreateIdleThreads();

            if (_scenario == Scenario.None)
            {
                await Task.Delay(Timeout.Infinite, _exitToken);
            }
            else
            {
                try
                {
                    List<string> asyncEndpoints = GetEndpoints(rootUrl);

                    // Run for the given number of iterations
                    // 0 means wait for cancellation
                    int current = 0;
                    while (
                        ((iterations == 0) && !_exitToken.IsCancellationRequested) ||
                        (iterations > current))
                    {
                        foreach (var asyncEndpoint in asyncEndpoints)
                        {
                            await ExecuteIterationAsync(asyncEndpoint);
                        }

                        await Task.Delay(SleepDuration);
                        current++;
                    }
                }
                catch (Exception x)
                {
                    Console.WriteLine($"{x.GetType().Name} | {x.Message}");
                }
            }

            Console.WriteLine($"{this.GetType().Name} stopped.");
        }

        private void CreateIdleThreads()
        {
            if (_nbIdleThreads == 0)
            {
                return;
            }

            Console.WriteLine($"----- Creating {_nbIdleThreads} idle threads");

            for (var i = 0; i < _nbIdleThreads; i++)
            {
                Task.Factory.StartNew(() => { _exitToken.WaitHandle.WaitOne(); }, TaskCreationOptions.LongRunning);
            }
        }

        private List<string> GetEndpoints(string rootUrl)
        {
            List<string> urls = new List<string>();
            if (_scenario == Scenario.None)
            {
                urls.Add($"{rootUrl}/Products");
            }
            else
            {
                if ((_scenario & Scenario.StringConcat) == Scenario.StringConcat)
                {
                    urls.Add($"{rootUrl}/Products");
                }

                if ((_scenario & Scenario.StringBuilder) == Scenario.StringBuilder)
                {
                    urls.Add($"{rootUrl}/Products/Builder");
                }

                if ((_scenario & Scenario.Parallel) == Scenario.Parallel)
                {
                    urls.Add($"{rootUrl}/Products/Parallel");
                }

                if ((_scenario & Scenario.Async) == Scenario.Async)
                {
                    urls.Add($"{rootUrl}/Products/async");
                }

                if ((_scenario & Scenario.FormatExceptions) == Scenario.FormatExceptions)
                {
                    urls.Add($"{rootUrl}/Products/Sales");
                }

                if ((_scenario & Scenario.ParallelLock) == Scenario.ParallelLock)
                {
                    urls.Add($"{rootUrl}/Products/ParallelLock");
                }

                if ((_scenario & Scenario.MemoryLeak) == Scenario.MemoryLeak)
                {
                    urls.Add($"{rootUrl}/News");
                }

                if ((_scenario & Scenario.EndpointsCount) == Scenario.EndpointsCount)
                {
                    urls.Add($"{rootUrl}/End.Point.With.Dots");
                }

                if ((_scenario & Scenario.Spin) == Scenario.Spin)
                {
                    urls.Add($"{rootUrl}/Products/IndexSlow");
                }
            }

            return urls;
        }

        private async Task ExecuteIterationAsync(string endpointUrl)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(endpointUrl, _exitToken);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Error] Request '{endpointUrl}' failed {response.StatusCode}");
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
