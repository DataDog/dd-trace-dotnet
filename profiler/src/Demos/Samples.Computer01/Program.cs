// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Demos.Util;
using Datadog.RuntimeMetrics;

namespace Samples.Computer01
{
    public enum Scenario
    {
        All,
        Computer,
        Generics,
        SimpleWallTime,
        PiComputation,
        FibonacciComputation,
        Sleep,
        Async,
        Iterator,
        GenericsAllocation,
        ContentionGenerator, // parameter = contention duration
        LinuxSignalHandler,
        GarbageCollection,   // parameter = generation 0, 1 or 2
        MemoryLeak,          // parameter = number of objects to allocate
        QuicklyDeadThreads,  // parameter = number of short lived threads to create
        LinuxMallocDeadlock,
        MeasureAllocations,
        InnerMethods,
        LineNumber,
        NullThreadNameBug,
        MethodSignature,
        OpenLdapCrash,
        SocketTimeout,
        ForceSigSegvHandler,
        Obfuscation,
        ThreadSpikes,
        StringConcat, // parameter = number of strings to concatenate
        LinuxDlIteratePhdrDeadlock,
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("######## Starting at " + DateTime.UtcNow);
            // supported scenarios:
            // --------------------
            //  0: all
            //  1: start threads with specific callstacks in another appdomain
            //  2: start threads with generic type and method having long parameters list in callstack
            //  3: start threads that sleep/task.delay for 10s, 20s, 30s, 40s every minute
            //  4: start a thread to compute pi at a certain precision (high CPU usage)
            //  5: start n threads computing fibonacci
            //  6: start n threads sleeping
            //  7: start n threads doing async calls with CPU consumption along the way
            //  8: start n threads doing iterator calls in constructors
            //  9: start n threads allocating array of Generic<int> in LOH
            // 10: start n threads waiting on the same lock
            // 11: linux signal handler
            // 12: start garbage collections of a given generation
            // 13: leak x LOH object with GC in between
            // 14: start n threads creating short lived threads
            // 15: trigger malloc deadlock on Linux
            // 16: count sized allocations
            // 17: generate frames with named and anonymous methods
            // 18: call stack on functions to check line number(s)
            // 19: set thread names to null and empty (validate bug fix)
            // 20: trigger exceptions with different method signatures in the callstack
            // 21: validate fix for OpenLDAP
            // 22: check socket timeout (linux)
            // 23: sigsegv handling validation
            // 24: use an obfuscated library
            // 25: create thread spikes
            // 26: string concatenation
            // 27: custom dl_iterate_phdr deadlock
            //
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} " +
            $"[--service] [--iterations <number of iterations to execute>] " +
            $"[--scenario <0=all 1=computer 2=generics 3=wall time 4=pi computation 5=compute fibonacci 6=n sleeping threads 7=async calls 8=iterator calls 9=allocate array of Generic<int>> 10=threads competing for a lock 11=lunix signal handler 12=trigger garbage collections 13=memory leak 14=short lived threads] " +
            $"[--param <any number to pass to the scenario - used for contention duration for example>] " +
            $"[--timeout <duration in seconds> | --run-infinitely]");
            Console.WriteLine();

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads, out int parameter);

            Console.WriteLine("Running Scenario " + scenario.ToString());
            // This application is used for several purposes:
            //  - execute a processing for a given duration (for smoke test)
            //  - execute a processing several times (for runtime test)
            //  - never stop (for reliability environment)
            //  - as a service
            //  - interactively for debugging
            var computerService = new ComputerService();

            if (runAsService)
            {
                computerService.RunAsService(timeout, scenario, parameter);
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

                        computerService.Run(scenario, iterations, nbThreads, parameter);
                    }
                    else
                    if (timeout == TimeSpan.MinValue)
                    {
                        Console.WriteLine($" ########### The application will run interactively because no timeout was specified or could be parsed. Number of Threads: {nbThreads}.");

                        computerService.StartService(scenario, nbThreads, parameter);

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to finish.");
                        Console.ReadLine();

                        computerService.StopService();

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to terminate.");
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine($" ########### The application will run non-interactively for {timeout} and will stop after that time. Number of Threads: {nbThreads}.");

                        computerService.StartService(scenario, nbThreads, parameter);

                        Thread.Sleep(timeout);

                        computerService.StopService();
                    }
                }

                Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
            }
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads, out int parameter)
        {
            timeout = TimeSpan.MinValue;
            runAsService = false;
            scenario = Scenario.PiComputation;
            iterations = 0;
            nbThreads = 1;
            parameter = int.MaxValue;
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
                else
                if ("--threads".Equals(arg, StringComparison.OrdinalIgnoreCase))
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
                else
                if ("--param".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        parameter = number;
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

            if (scenario == Scenario.LinuxSignalHandler &&
                (Environment.OSVersion.Platform != PlatformID.Unix ||
                Environment.Version.Major < 6))
            {
                throw new InvalidOperationException($"Scenario LinuxSignalHandler can only run on Linux and .NET 6.0");
            }
        }
    }
}
