// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Threading;

namespace AllocSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ParseCommandLine(args, out string allocFile, out string allocDirectory);
            // TODO support scanning a directory

            var provider = new FileAllocProvider(allocFile);
            var sampler = new AllocSampler();
            Engine engine = new Engine(provider, sampler);
            engine.Run();
            var realAllocations = engine.GetAllocations();
            var sampledAllocations = sampler.GetAllocs().ToList();

            foreach (var allocation in realAllocations)
            {
                var realKey = $"{allocation.Type}+{allocation.Key}";
                Console.WriteLine($"{allocation.Count,9} | {allocation.Size,13} - {realKey}");
                var sampled = sampledAllocations.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                if (sampled != null)
                {
                    Console.WriteLine($"{sampled.Count,9} | {sampled.Size,13}");
                }
                else
                {
                    Console.WriteLine($"        ~ |-----------------^");
                }

                Console.WriteLine();
            }
        }

        private static void ParseCommandLine(string[] args, out string allocFile, out string allocDirectory)
        {
            allocFile = string.Empty;
            allocDirectory = string.Empty;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if ("-d".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        allocDirectory = args[i];
                    }
                }
                else
                {
                    allocFile = arg;
                }
            }
        }
    }
}
