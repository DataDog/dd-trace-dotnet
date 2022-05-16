// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading;
using Datadog.TestUtil;

namespace Samples.ExceptionGenerator
{
    public enum Scenario
    {
        ExceptionsProfilerTest = 1,
        ParallelExceptions = 2
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Starting at {DateTime.UtcNow}");
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} [--service] [--timeout TimeoutInSeconds | --run-infinitely | --scenario Scenario]");
            Console.WriteLine();

            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out var scenario, out bool runAsService);

            var exceptionGeneratorService = new ExceptionGeneratorService();

            if (runAsService)
            {
                exceptionGeneratorService.RunAsService(timeout);
            }
            else
            {
                if (scenario != null)
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
                            Console.WriteLine(" ########### Sleeping for 10 seconds");
                            Thread.Sleep(20_000);
                            break;

                        default:
                            Console.WriteLine($" ########### Unknown scenario: {scenario}.");
                            break;
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

                Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
            }
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out Scenario? scenario, out bool runAsService)
        {
            timeout = TimeSpan.MinValue;
            runAsService = false;
            scenario = default;

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

                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < i + 1 || !int.TryParse(args[i + 1], out var scenarioIndex))
                    {
                        throw new InvalidOperationException("Missing scenario or invalid number");
                    }

                    scenario = (Scenario)scenarioIndex;
                }

                if ("--run-infinitely".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    timeout = Timeout.InfiniteTimeSpan;
                }

                if ("--service".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    runAsService = true;
                }
            }
        }
    }
}
