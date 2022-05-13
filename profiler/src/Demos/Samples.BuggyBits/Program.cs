// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace BuggyBits
{
    public enum Scenario
    {
        StringConcat,
        StringBuilder,
        Parallel,
        Async,
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var sw = new Stopwatch();

            WriteLine("Starting at " + DateTime.UtcNow);

            ParseCommandLine(args, out var timeout, out var iterations, out var scenario);

            using (var host = CreateHostBuilder(args).Build())
            {
                // ASP.NET Core accepts listening url via what is set by Visual Studio
                // (from the launchsettings.json). It could be overriden by --Urls
                // on the command line
                var configuration = host.Services.GetService(typeof(IConfiguration)) as IConfiguration;
                var rootUrl = configuration["urls"];

                // otherwise, use the default ASP.NET Core value
                if (string.IsNullOrEmpty(rootUrl))
                {
                    rootUrl = "http://localhost:5000";
                }

                WriteLine($"Listening to {rootUrl}");

                var cts = new CancellationTokenSource();
                using (var selfInvoker = new SelfInvoker(cts.Token, scenario))
                {
                    await host.StartAsync();

                    WriteLine();
                    WriteLine($"Started at {DateTime.UtcNow}.");

                    sw.Start();

                    if (iterations > 0)
                    {
                        await selfInvoker.RunAsync(rootUrl, iterations);
                        return;
                    }

                    var selfInvokerTask = selfInvoker.RunAsync(rootUrl);

                    // allow interaction with user
                    if (timeout == TimeSpan.MinValue)
                    {
                        WriteLine("Press ENTER to exit...");
                        Console.ReadLine();
                    }
                    else
                    {
                        // otherwise, wait for a specific duration before exiting
                        // or wait forever (for linux support)
                        if (timeout == Timeout.InfiniteTimeSpan)
                        {
                            WriteLine($"The application will run non-interactively forever.");
                        }
                        else
                        {
                            WriteLine($"The application will run non-interactively for {timeout} and exit.");
                        }

                        Thread.Sleep(timeout);
                    }

                    WriteLine("Stopping...");
                    cts.Cancel();
                    try
                    {
                        await selfInvokerTask;
                        await host.StopAsync();
                    }
                    catch (Exception x)
                    {
                        WriteLine($"Error while stopping host: {x.GetType().Name} | {x.Message}");
                    }
                }
            }

            sw.Stop();
            WriteLine($"The application exited after: {sw.Elapsed} at {DateTime.UtcNow}");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out int iterations, out Scenario scenario)
        {
            // by default, need interactive action to exit and string.Concat scenario
            timeout = TimeSpan.MinValue;
            iterations = 0;
            scenario = Scenario.StringConcat;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int iterationsArgument = i + 1;
                    if (iterationsArgument >= args.Length || !int.TryParse(args[iterationsArgument], out var number))
                    {
                        throw new InvalidOperationException($"Invalid or missing scenario after --scenario");
                    }

                    scenario = (Scenario)number;
                }
                else
                if ("--iterations".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int iterationsArgument = i + 1;
                    if (iterationsArgument >= args.Length || !int.TryParse(args[iterationsArgument], out iterations))
                    {
                        throw new InvalidOperationException($"Invalid or missing count after --iterations");
                    }
                }
                else
                if ("--timeout".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int timeoutArgument = i + 1;
                    if (timeoutArgument < args.Length && int.TryParse(args[timeoutArgument], out var timeoutInSecond))
                    {
                        timeout = TimeSpan.FromSeconds(timeoutInSecond);
                    }
                    else
                    {
                        // default 30 seconds lifetime if error in command line
                        timeout = TimeSpan.FromSeconds(30);
                        WriteLine($"Invalid or missing duration after --timeout: default 30 seconds lifetime");
                    }
                }
                else
                if ("--run-infinitely".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    timeout = Timeout.InfiniteTimeSpan;
                }
            }
        }

        private static void WriteLine(string line = null)
        {
            if (line == null)
            {
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($" ########### {line}");
            }
        }
    }
}
