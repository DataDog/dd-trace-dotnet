// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Datadog.TestUtil;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Datadog.Demos.Website_AspNetCore01
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var appDuration = new Stopwatch();
            appDuration.Start();

            WriteLine();
            WriteLine($"Starting at {DateTime.UtcNow}");
            WriteLine("   ==> Will NOT manually call profiler engine entry point.");

            EnvironmentInfo.PrintDescriptionToConsole();

            var hasTimeout = TryExtractTimeoutFromCommandLineArguments(args, out TimeSpan timeout);
            if (hasTimeout)
            {
                WriteLine($"The application will run non-interactively for {timeout} and will stop after that time.");
            }
            else
            {
                timeout = Timeout.InfiniteTimeSpan;
                WriteLine("The application will run interactively because no timeout was specified or could be parsed.");
            }

            var sw = new Stopwatch();
            sw.Start();
            using (IHost host = CreateHostBuilder(args).Build())
            {
                sw.Stop();
                WriteLine($"host built in {sw.ElapsedMilliseconds} ms");
                sw.Restart();

                // ASP.NET Core accepts listening url via what is set by Visual Studio
                // (from the launchsettings.json). It could be overriden by --Urls
                // on the command line
                var configuration = host.Services.GetService(typeof(IConfiguration)) as IConfiguration;
                var rootUrl = configuration["Urls"];

                // otherwise, use the default ASP.NET Core value
                if (string.IsNullOrEmpty(rootUrl))
                {
                    rootUrl = "http://localhost:5000";
                }

                WriteLine($"Listening to {rootUrl}");

                var cts = new CancellationTokenSource();
                using (var selfInvoker = new SelfInvoker(cts.Token))
                {
                    sw.Stop();
                    WriteLine($"SelfInvoker built in {sw.ElapsedMilliseconds} ms");
                    sw.Restart();

                    await host.StartAsync();

                    sw.Stop();
                    WriteLine($"host started in {sw.ElapsedMilliseconds} ms");

                    WriteLine();
                    WriteLine($"Started at {DateTime.UtcNow}.");

                    Task selfInvokerTask = selfInvoker.RunAsync(rootUrl);

                    if (hasTimeout)
                    {
                        Thread.Sleep(timeout);
                    }
                    else
                    {
                        WriteLine();
                        WriteLine("Press enter to finish.");
                        Console.ReadLine();
                    }

                    WriteLine($"Stopping... ");
                    cts.Cancel();

                    await selfInvokerTask;
                    await host.StopAsync();
                }
            }

            appDuration.Stop();
            WriteLine($"The application run {appDuration.Elapsed} and exited {DateTime.UtcNow}");
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });

        // Helper method to output both to the console (when the app runs in the console)
        // and via Trace to see them while running under IISExpress both in Visual Studio
        // or with SysInternals DebugView (https://docs.microsoft.com/en-us/sysinternals/downloads/debugview)
        private static void WriteLine(string line = null)
        {
            if (line == null)
            {
                Trace.WriteLine(string.Empty);
                Console.WriteLine();
            }
            else
            {
                Trace.WriteLine($" ########### {line}");
                Console.WriteLine($" ########### {line}");
            }
        }

        private static bool TryExtractTimeoutFromCommandLineArguments(string[] args, out TimeSpan timeout)
        {
            if (args.Length > 0)
            {
                if (args.Length > 1 && "--timeout".Equals(args[0]))
                {
                    if (int.TryParse(args[1], out var timeoutInSecond))
                    {
                        timeout = TimeSpan.FromSeconds(timeoutInSecond);
                        return true;
                    }
                }
            }

            timeout = TimeSpan.MinValue;
            return false;
        }
    }
}
