// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Demos.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;

namespace Samples.Website_AspNetCore01
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

                var cts = new CancellationTokenSource();
                using (var selfInvoker = new SelfInvoker(cts.Token))
                {
                    sw.Stop();
                    WriteLine($"SelfInvoker built in {sw.ElapsedMilliseconds} ms");
                    sw.Restart();

                    await host.StartAsync();

                    sw.Stop();
                    WriteLine($"host started in {sw.ElapsedMilliseconds} ms");

                    // Read the address Kestrel actually bound (after StartAsync). Configuration["Urls"]
                    // may be "http://127.0.0.1:0" when the test runner asks for a dynamic port, so the
                    // bound address is the only reliable source of the real listening URL.
                    var rootUrl = GetBoundRootUrl(host);
                    WriteLine($"Listening to {rootUrl}");

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

        private static string GetBoundRootUrl(IHost host)
        {
            var server = (IServer)host.Services.GetService(typeof(IServer));
            var address = server?.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
            return string.IsNullOrEmpty(address) ? "http://localhost:5000" : address.TrimEnd('/');
        }

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
