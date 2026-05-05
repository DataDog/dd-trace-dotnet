// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Demos.Util;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
        Spin = 256, // Requests that take a long time
        Redirect = 512, // triggers HTTP redirect
        GetAwaiterGetResult = 1024, // using GetAwaiter().GetResult() instead of await
        UseResultProperty = 2048, // using Result property instead of GetAwaiter().GetResult()
        ShortLived = 4096,      // short lived threads
    }

    public class Program
    {
        private const int HostBindRetries = 5;

        private static bool _disableLogs = false;

        public static async Task Main(string[] args)
        {
            var sw = new Stopwatch();

            WriteLine("Starting at " + DateTime.UtcNow);

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out _disableLogs, out var timeout, out var iterations, out var scenario, out var nbIdleThreads);

            using (var host = await StartHostWithPortResilience(args))
            {
                var rootUrl = GetBoundRootUrl(host);
                WriteLine($"Listening to {rootUrl}");

                var cts = new CancellationTokenSource();
                using (var selfInvoker = new SelfInvoker(cts.Token, scenario, nbIdleThreads, _disableLogs))
                {
                    WriteLine();
                    WriteLine($"Started at {DateTime.UtcNow}.");

                    // uncomment when needed to attach an event listener before the scenarios run
                    Console.WriteLine($"pid = {Process.GetCurrentProcess().Id}");
                    // Console.ReadLine();

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

        private static async Task<IHost> StartHostWithPortResilience(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            try
            {
                var rootUrl = GetConfiguredRootUrl(host);

                for (var remainingRetries = HostBindRetries; ; remainingRetries--)
                {
                    try
                    {
                        WriteLine($"Starting web host at {rootUrl}");
                        await host.StartAsync();
                        return host;
                    }
                    catch (Exception ex) when (remainingRetries > 0 && IsAddressInUseException(ex))
                    {
                        var retryRootUrl = GetUrlWithNewPort(rootUrl);
                        WriteLine($"Address already in use for {rootUrl}. Retrying on {retryRootUrl}.");

                        host.Dispose();
                        rootUrl = retryRootUrl;
                        host = CreateHostBuilder(args, rootUrl).Build();
                    }
                }
            }
            catch
            {
                host.Dispose();
                throw;
            }
        }

        private static string GetConfiguredRootUrl(IHost host)
        {
            // ASP.NET Core accepts listening urls from launchSettings.json, ASPNETCORE_URLS,
            // or --urls. If none are supplied, Kestrel uses this default.
            var configuration = host.Services.GetService(typeof(IConfiguration)) as IConfiguration;
            var rootUrl = configuration?["urls"];

            if (string.IsNullOrEmpty(rootUrl))
            {
                rootUrl = "http://localhost:5000";
            }

            return GetFirstUrl(rootUrl);
        }

        private static string GetBoundRootUrl(IHost host)
        {
            var server = host.Services.GetService(typeof(IServer)) as IServer;
            var addresses = server?.Features.Get<IServerAddressesFeature>()?.Addresses;

            if (addresses != null)
            {
                foreach (var address in addresses)
                {
                    if (!string.IsNullOrEmpty(address))
                    {
                        return address.TrimEnd('/');
                    }
                }
            }

            return GetConfiguredRootUrl(host);
        }

        private static string GetFirstUrl(string urls)
        {
            var separatorIndex = urls.IndexOf(';');
            if (separatorIndex >= 0)
            {
                urls = urls.Substring(0, separatorIndex);
            }

            return urls.TrimEnd('/');
        }

        private static string GetUrlWithNewPort(string rootUrl)
        {
            // Avoid UriBuilder here: ASP.NET Core wildcard hosts (http://*:5000, http://+:5000)
            // are not valid Uri hosts and would throw UriFormatException. Since the retry always
            // rebinds to 127.0.0.1 with an OS-assigned port, only the scheme needs to be preserved.
            var schemeSeparator = rootUrl.IndexOf("://", StringComparison.Ordinal);
            var scheme = schemeSeparator > 0 ? rootUrl.Substring(0, schemeSeparator) : "http";
            return $"{scheme}://127.0.0.1:0";
        }

        private static bool IsAddressInUseException(Exception exception)
        {
            while (exception != null)
            {
                if (exception is SocketException socketException && socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return true;
                }

                if (exception.Message.IndexOf("address already in use", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                exception = exception.InnerException;
            }

            return false;
        }

        public static IHostBuilder CreateHostBuilder(string[] args, string rootUrl = null) =>
            Host.CreateDefaultBuilder(GetArgsWithUrlsOverride(args, rootUrl))
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging((context, logging) =>
                {
                    if (_disableLogs)
                    {
                        logging.ClearProviders();
                    }
                });

        private static string[] GetArgsWithUrlsOverride(string[] args, string rootUrl)
        {
            if (string.IsNullOrEmpty(rootUrl))
            {
                return args;
            }

            var effectiveArgs = new List<string>(args.Length + 2);
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (string.Equals(arg, "--urls", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }

                if (arg.StartsWith("--urls=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                effectiveArgs.Add(arg);
            }

            effectiveArgs.Add("--urls");
            effectiveArgs.Add(rootUrl);
            return effectiveArgs.ToArray();
        }

        private static void ParseCommandLine(string[] args, out bool disableLogs, out TimeSpan timeout, out int iterations, out Scenario scenario, out int nbIdleThreads)
        {
            // by default, need interactive action to exit and string.Concat scenario
            timeout = TimeSpan.MinValue;
            iterations = 0;
            scenario = Scenario.StringConcat;
            nbIdleThreads = 0;
            disableLogs = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if ("--disableLogs".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    disableLogs = true;
                }
                else
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
