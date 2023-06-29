// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Demos.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// Those attributes are required to validate the git repository url and commit sha propagation from
// the tracer to the profiler.
// See Datadog.Profiler.IntegrationTests.GitMetadata.CheckGitMetataFromEnvironmentVariablesFromBinary
[assembly: System.Reflection.AssemblyMetadata("RepositoryUrl", "http://github.com/DataDog/dd-trace-dotnet")]
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.0.0+1234567890ABCDEF")]

namespace BuggyBits
{
    public enum Scenario
    {
        None = 0,
        StringConcat = 1,      // using += / String.Concat
        StringBuilder = 2,     // using StringBuilder
        Parallel = 4,          // using parallel code
        Async = 8,             // using async code
        FormatExceptions = 16, // generating FormatExceptions for prices
        ParallelLock = 32,     // using parallel code with lock
        MemoryLeak = 64, // keep a controller in memory due to instance callback passed to a cache
        EndpointsCount = 128, // Specific test with '.' in endpoint name
        Spin = 256 // Requests that take a long time
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var sw = new Stopwatch();

            WriteLine("Starting at " + DateTime.UtcNow);

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out var timeout, out var iterations, out var scenario, out var nbIdleThreads);

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
                using (var selfInvoker = new SelfInvoker(cts.Token, scenario, nbIdleThreads))
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
                    catch (OperationCanceledException ocx)
                    {
                        WriteLine($"Operation cancelled while stopping host: {ocx.Message}");
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

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out int iterations, out Scenario scenario, out int nbIdleThreads)
        {
            // by default, need interactive action to exit and string.Concat scenario
            timeout = TimeSpan.MinValue;
            iterations = 0;
            scenario = Scenario.StringConcat;
            nbIdleThreads = 0;

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
                else
                if ("--with-idle-threads".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    var nbThreadsArgument = i + 1;
                    if (nbThreadsArgument >= args.Length || !int.TryParse(args[nbThreadsArgument], out nbIdleThreads))
                    {
                        throw new InvalidOperationException($"Invalid or missing count after --with-idle-threads");
                    }
                }
            }

            // sanity checks
            if ((scenario == 0) && (iterations > 0))
            {
                throw new InvalidOperationException("It is not possible to iterate on scenario 0");
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
