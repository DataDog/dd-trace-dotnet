// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Demos.Util;
using Datadog.RuntimeMetrics;

namespace Samples.ExceptionGenerator
{
    public enum Scenario
    {
        ExceptionsProfilerTest = 1,
        ParallelExceptions = 2,
        Sampling = 3,
        GenericExceptions = 4,
        TimeItExceptions = 5,
        MeasureExceptions = 6,
        Unhandled = 7
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Starting at {DateTime.UtcNow}");
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} [--service] [--timeout TimeoutInSeconds | --run-infinitely | --scenario Scenario]");
            Console.WriteLine();

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out var scenario, out var iterations, out bool runAsService);

            var exceptionGeneratorService = new ExceptionGeneratorService();

            if (runAsService)
            {
                exceptionGeneratorService.RunAsService(timeout);
            }
            else
            {
                // collect CLR metrics that will be saved into a json file
                // if DD_PROFILING_METRICS_FILEPATH is set
                using (var collector = new MetricsCollector())
                {
                    if (scenario != null)
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            switch (scenario.Value)
                            {
                                case Scenario.ExceptionsProfilerTest:
                                    new ExceptionsProfilerTestScenario().Run();

                                    // TODO: Remove the sleep when flush on shutdown is implemented in the profiler
                                    Console.WriteLine(" ########### Sleeping for 10 seconds");
                                    Thread.Sleep(10_000);
                                    break;

                                case Scenario.ParallelExceptions:
                                    new ParallelExceptionsScenario().Run();

                                    // TODO: Remove the sleep when flush on shutdown is implemented in the profiler
                                    Console.WriteLine(" ########### Sleeping for 20 seconds");
                                    Thread.Sleep(20_000);
                                    break;

                                case Scenario.Sampling:
                                    new SamplingScenario().Run();

                                    // TODO: Remove the sleep when flush on shutdown is implemented in the profiler
                                    Console.WriteLine(" ########### Sleeping for 20 seconds");
                                    Thread.Sleep(20_000);
                                    break;

                                case Scenario.GenericExceptions:
                                    new GenericExceptionsScenario().Run();
                                    Console.WriteLine(" ########### Generating generic exceptions...");
                                    break;

                                case Scenario.TimeItExceptions:
                                    new ParallelExceptionsScenario().Run();
                                    Console.WriteLine(" ########### Generating exceptions in parallel...");
                                    break;

                                case Scenario.MeasureExceptions:
                                    new MeasureExceptionsScenario().Run();
                                    Console.WriteLine(" ########### Measuring exceptions...");
                                    break;

                                case Scenario.Unhandled:
                                    Console.WriteLine(" ########### Crashing...");
                                    throw new InvalidOperationException("Task failed successfully.");

                                default:
                                    Console.WriteLine($" ########### Unknown scenario: {scenario}.");
                                    break;
                            }
                        }
                    }
                    else if (timeout == TimeSpan.MinValue)
                    {
                        Console.WriteLine($" ########### The application will run interactively because no timeout was specified or could be parsed.");

                        exceptionGeneratorService.StartService();

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to finish.");
                        Console.ReadLine();

                        exceptionGeneratorService.StopService();

                        Console.WriteLine($"{Environment.NewLine} ########### Press enter to terminate.");
                        Console.ReadLine();
                    }
                    else
                    {
                        Console.WriteLine($" ########### The application will run non-interactively for {timeout} and will stop after that time.");

                        exceptionGeneratorService.StartService();

                        Thread.Sleep(timeout);

                        exceptionGeneratorService.StopService();
                    }
                }

                Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
            }
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out Scenario? scenario, out int iterations, out bool runAsService)
        {
            timeout = TimeSpan.MinValue;
            runAsService = false;
            scenario = default;
            iterations = 0;

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
                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < i + 1 || !int.TryParse(args[i + 1], out var scenarioIndex))
                    {
                        throw new InvalidOperationException("Missing scenario or invalid number");
                    }

                    scenario = (Scenario)scenarioIndex;
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
                if ("--run-infinitely".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    timeout = Timeout.InfiniteTimeSpan;
                }
                else
                if ("--service".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    runAsService = true;
                }
            }

            // check consistency in parameters:
            //  - can't have both --iterations and --duration
            //  - can't have both --service and --iterations
            //  - if --scenario but not --iterations, iterations = 1
            if ((iterations != 0) && (timeout != TimeSpan.MinValue))
            {
                throw new InvalidOperationException("Both --iterations and --duration are not supported");
            }

            if ((iterations != 0) && runAsService)
            {
                throw new InvalidOperationException("Both --iterations and --service are not supported");
            }

            if ((iterations == 0) && scenario != null)
            {
                iterations = 1;
            }
        }
    }
}
