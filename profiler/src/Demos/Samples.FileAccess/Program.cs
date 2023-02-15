// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Demos.Util;

namespace Samples.FileAccess
{
    public enum Scenario
    {
        ReadWriteBinary = 1,
        ReadWriteText = 2,
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("######## Starting at " + DateTime.UtcNow);

            // supported scenarios:
            // --------------------
            //  1: synchronous read and write binary data
            //  1: synchronous read and write textual data
            //
            Console.WriteLine($"{Environment.NewLine}Usage:{Environment.NewLine} > {Process.GetCurrentProcess().ProcessName} " +
            $"[--iterations <number of iterations to execute>] " +
            $"[--scenario <1=read/write binary 2=read/write text] " +
            $"[--param <any number to pass to the scenario - used for contention duration for example>] " +
            $"[--timeout <duration in seconds>]");
            Console.WriteLine();
            EnvironmentInfo.PrintDescriptionToConsole();

            ParseCommandLine(args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads, out int parameter);

            var cts = new CancellationTokenSource();

            var tasks = StartScenarios(scenario, cts.Token);

            if (timeout == TimeSpan.MinValue)
            {
                Console.WriteLine("Press ENTER to exit...");
                Console.ReadLine();
            }
            else
            {
                Thread.Sleep(timeout);
            }

            cts.Cancel();
            Task.WhenAll(tasks).Wait();

            Console.WriteLine($"{Environment.NewLine} ########### Finishing run at {DateTime.UtcNow}");
        }

        private static List<Task> StartScenarios(Scenario scenario, CancellationToken token)
        {
            List<Task> tasks = new List<Task>();
            if ((scenario & Scenario.ReadWriteBinary) == Scenario.ReadWriteBinary)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            ReadWriteBinary(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

            if ((scenario & Scenario.ReadWriteText) == Scenario.ReadWriteText)
            {
                tasks.Add(
                    Task.Factory.StartNew(
                        () =>
                        {
                            ReadWriteText(token);
                        },
                        TaskCreationOptions.LongRunning));
            }

            return tasks;
        }

        private static void ReadWriteBinary(CancellationToken token)
        {
            var filename = Path.GetTempFileName();
            while (!token.IsCancellationRequested)
            {
                using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                {
                    using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
                    {
                        writer.Write(DateTime.Now.ToShortTimeString());
                        for (Int32 i = 0; i < 10_000; i++)
                        {
                            writer.Write(i);
                        }

                        writer.Write("This is the end");
                    }
                }

                if (token.IsCancellationRequested)
                {
                    break;
                }

                using (var stream = File.Open(filename, FileMode.OpenOrCreate))
                {
                    using (var writer = new BinaryReader(stream, Encoding.UTF8, false))
                    {
                        writer.ReadString();
                        for (int i = 0; i < 10_000; i++)
                        {
                            writer.ReadInt32();
                        }

                        writer.ReadString();
                    }
                }
            }

            File.Delete(filename);
        }

        private static void ReadWriteText(CancellationToken token)
        {
        }

        private static void ParseCommandLine(string[] args, out TimeSpan timeout, out bool runAsService, out Scenario scenario, out int iterations, out int nbThreads, out int parameter)
        {
            timeout = TimeSpan.MinValue;
            runAsService = false;
            scenario = Scenario.ReadWriteBinary;
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
                if ("--scenario".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    int valueOffset = i + 1;
                    if (valueOffset < args.Length && int.TryParse(args[valueOffset], out var number))
                    {
                        scenario = (Scenario)number;
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
        }
    }
}
