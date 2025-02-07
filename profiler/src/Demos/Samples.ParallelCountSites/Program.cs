// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Samples.ParallelCountSites
{
    [Flags]
    public enum Scenario
    {
        Redirect = 1,
        Invalid = 2,
        Blog = 4,

        All = Redirect | Invalid | Blog
    }

    internal class Program
    {
        public static async Task Main(string[] args)
        {
            ParseCommandLine(args, out var iterations, out var scenario);

            await Console.Out.WriteLineAsync($"pid = {Process.GetCurrentProcess().Id}");
            await Console.Out.WriteLineAsync();

            // await Console.In.ReadLineAsync();

            var downloader = new Downloader(scenario);
            await downloader.SumPagesSize(iterations);
        }

        private static void ParseCommandLine(string[] args, out int iterations, out Scenario scenario)
        {
            iterations = 5;
            scenario = Scenario.All;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
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
            }
        }
    }
}
