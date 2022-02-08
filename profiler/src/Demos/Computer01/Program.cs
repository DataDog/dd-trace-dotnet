// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.RuntimeMetrics;
using Datadog.TestUtil;

namespace Datadog.Demos.Computer01
{
    public enum Scenario
    {
        All,
        Computer,
        Generics,
        SimpleWallTime,
        PiComputation,
        FibonacciComputation,
        Sleep
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("######## Starting at " + DateTime.UtcNow);
            // supported scenarios:
            // --------------------
            // 0: all
            // 1: start threads with specific callstacks in another appdomain
            // 2: start threads with generic type and method having long parameters list in callstack
            // 3: start threads that sleep/task.delay for 10s, 20s, 30s, 40s every minute
            // 4: start a thread to compute pi at a certain precision (high CPU usage)
            // 5: start n threads computing fibonacci
            // 6: start n threads sleeping
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} [--service] [--iterations <number of iterations to execute>] [--scenario <0=all 1=computer 2=generics 3=wall time 4=pi computation>] [--timeout <duration in seconds> | --run-infinitely]");
            Console.WriteLine();

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads);

            // This application is used for several purposes:
            //  - execute a processing for a given duration (for smoke test)
            //  - execute a processing several times (for runtime test)
            //  - never stop (for reliability environment)
            //  - as a service
            //  - interactively for debugging
            var computerService = new ComputerService();

            if (runAsService)
            {
                computerService.RunAsService(timeout, scenario);
            }
            else
            {
                // collect CLR metrics that will be saved into a json file
                // if DD_PROFILING_METRICS_FILEPATH is set
                using (var collector = new MetricsCollector())
                {
                    if (iterations > 0)
                    {
                        Console.WriteLine($" ########### The application will run scenario {scenario} {iterations} times with {nbThreads} thread(s).");

                        computerService.Run(scenario, iterations, nbThreads);
                    }
                    else
                    if (timeout == TimeSpan.MinValue)
                    {
                        Console.WriteLine($" ########### The application will run interactively because no timeout was specified or could be parsed. Number of Threads: {nbThreads}.");

                        computerService.StartService(scenario, nbThreads);

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to finish.");
                        Console.ReadLine();

                        computerService.StopService();

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to terminate.");
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine($" ########### The application will run non-interactively for {timeout} and will stop after that time. Number of Threads: {nbThreads}.");

                        computerService.StartService(scenario, nbThreads);

                        Thread.Sleep(timeout);

                        computerService.StopService();
                    }
                }

                Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
            }
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads)
        {
            timeout = TimeSpan.MinValue;
            runAsService = false;
            scenario = Scenario.PiComputation;
            iterations = 0;
            nbThreads = 1;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if ("--timeout".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var timeoutInSecond))
                    {
                        timeout = TimeSpan.FromSeconds(timeoutInSecond);
                    }
                }
                else
                if ("--run-infinitely".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    timeout = Timeout.InfiniteTimeSpan;
                }
                else
                if ("--service".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    runAsService = true;
                }
                else
                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        scenario = (Scenario)number;
                    }
                }
                else
                if ("--iterations".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        if (number <= 0)
                        {
                            throw new ArgumentOutOfRangeException($"Invalid iterations count '{number}': must be > 0");
                        }

                        iterations = number;
                    }
                }
                else if ("--threads".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        if (number <= 0)
                        {
                            throw new ArgumentOutOfRangeException($"Invalid numbers of threads '{number}: must be > 0");
                        }

                        nbThreads = number;
                    }
                }
            }

            // check consistency in parameters:
            //  - can't have both --iterations and --duration
            //  - can't have both --service and --iterations
            if ((iterations != 0) && (timeout != TimeSpan.MinValue))
            {
                throw new InvalidOperationException("Both --iterations and --duration are not supported");
            }

            if ((iterations != 0) && runAsService)
            {
                throw new InvalidOperationException("Both --iterations and --service are not supported");
            }
        }
    }
}
