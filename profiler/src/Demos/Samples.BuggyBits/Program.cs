// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Demos.Util;
using Microsoft.AspNetCore.Hosting;
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
        private static bool _disableLogs = false;

        public static async Task Main(string[] args)
        {
            var sw = new Stopwatch();

            WriteLine("Starting at " + DateTime.UtcNow);

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out _disableLogs, out var timeout, out var iterations, out var scenario, out var nbIdleThreads);

            // Resolve the URL/port before building the host so that Kestrel is configured
            // with a port that is actually free at bind time. This avoids a TOCTOU race:
            // previously the host was built with the original --urls port, and the
            // GetValidPort probe ran only after the build (too late to affect Kestrel).
            args = ResolveListenUrl(args, out var rootUrl);
            WriteLine($"Listening to {rootUrl}");

            using (var host = CreateHostBuilder(args).Build())
            {
                var cts = new CancellationTokenSource();
                using (var selfInvoker = new SelfInvoker(cts.Token, scenario, nbIdleThreads, _disableLogs))
                {
                    await host.StartAsync();

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

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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

        public static int GetOpenPort()
        {
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(IPAddress.Loopback, 0);
                tcpListener.Start();
                var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
                return port;
            }
            finally
            {
                tcpListener?.Stop();
            }
        }

        /// <summary>
        /// Resolves the Kestrel listen URL by finding a free port before the host is built.
        /// This ensures <see cref="CreateHostBuilder"/> receives the correct port in
        /// <paramref name="args"/> so that Kestrel binds without a race.
        /// </summary>
        /// <param name="args">Original command-line args (may contain --urls).</param>
        /// <param name="resolvedUrl">The resolved URL with a free port substituted in.</param>
        /// <returns>Updated args array where --urls points to the resolved URL.</returns>
        private static string[] ResolveListenUrl(string[] args, out string resolvedUrl)
        {
            // Extract the --urls value from command-line args (ASP.NET Core convention).
            // Fall back to the default Kestrel URL if not specified.
            string urlFromArgs = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--urls", StringComparison.OrdinalIgnoreCase))
                {
                    urlFromArgs = args[i + 1];
                    break;
                }
            }

            var baseUrl = urlFromArgs ?? "http://localhost:5000";

            // Find a free port (up to 5 attempts with fresh ephemeral ports on each retry).
            resolvedUrl = FindFreePortUrl(baseUrl, retries: 5);

            // Replace (or inject) --urls so CreateHostBuilder configures Kestrel correctly.
            return ReplaceUrlInArgs(args, resolvedUrl);
        }

        /// <summary>
        /// Returns <paramref name="baseUrl"/> with its port component replaced by the first
        /// available port found within <paramref name="retries"/> attempts.
        /// </summary>
        private static string FindFreePortUrl(string baseUrl, int retries)
        {
            var lastColon = baseUrl.LastIndexOf(':');
            if (lastColon < 0 || !int.TryParse(baseUrl.Substring(lastColon + 1), out int initialPort))
            {
                // No explicit port — return as-is and let Kestrel use its default.
                return baseUrl;
            }

            var urlPrefix = baseUrl.Substring(0, lastColon + 1); // e.g. "http://localhost:"
            var port = initialPort;

            for (int attempt = 0; attempt <= retries; attempt++)
            {
                if (IsPortAvailable(port))
                {
                    return urlPrefix + port;
                }

                // Port is busy — pick a fresh ephemeral port for the next attempt.
                port = GetOpenPort();
            }

            // All retries exhausted — use the last candidate and surface a real error if
            // it is still busy (better than silently starting on the wrong port).
            return urlPrefix + port;
        }

        /// <summary>
        /// Returns true if <paramref name="port"/> appears to be free on the loopback
        /// interface; false if it is already in use.
        /// </summary>
        private static bool IsPortAvailable(int port)
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Prefixes.Add($"http://localhost:{port}/");
            try
            {
                listener.Start();
                return true;
            }
            catch (HttpListenerException)
            {
                return false;
            }
            finally
            {
                listener.Close();
            }
        }

        /// <summary>
        /// Returns a copy of <paramref name="args"/> where the value after --urls is
        /// replaced with <paramref name="newUrl"/>.  Appends --urls newUrl if the flag
        /// is not already present.
        /// </summary>
        private static string[] ReplaceUrlInArgs(string[] args, string newUrl)
        {
            var list = new System.Collections.Generic.List<string>(args);
            for (int i = 0; i < list.Count - 1; i++)
            {
                if (list[i].Equals("--urls", StringComparison.OrdinalIgnoreCase))
                {
                    list[i + 1] = newUrl;
                    return list.ToArray();
                }
            }

            // "--urls" not found — append so Kestrel picks up the resolved port.
            list.Add("--urls");
            list.Add(newUrl);
            return list.ToArray();
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
