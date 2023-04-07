// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            ParseCommandLine(args, out string allocFile, out string allocDirectory, out int meanPoisson);

            if (string.IsNullOrEmpty(allocDirectory))
            {
                SimulateAllocations(allocFile, meanPoisson);
                return;
            }

            foreach (var filename in Directory.GetFiles(allocDirectory, "*.alloc"))
            {
                SimulateAllocations(filename, meanPoisson);
            }
        }

        private static void SimulateAllocations(string allocFile, int meanPoisson)
        {
            try
            {
                // accept either text (.alloc) or binary (.balloc) allocations set
                IAllocProvider provider;
                var extension = Path.GetExtension(allocFile);
                if (extension == ".alloc")
                {
                    provider = new TextFileAllocProvider(allocFile);
                }
                else if (extension == ".balloc")
                {
                    provider = new BinaryFileAllocProvider(allocFile);
                }
                else
                {
                    throw new InvalidOperationException($"{extension} file extension is not supported");
                }

                var filename = Path.GetFileNameWithoutExtension(allocFile);
                Console.WriteLine($"Simulate allocations - {filename}");
                Console.WriteLine("   Fixed 100 KB sampling");
                var sampler = new FixedSampler();
                var upscaler = new FixedUpscaler();
                Engine engine = new Engine(provider, sampler, upscaler);
                engine.Run();
                var realAllocations = engine.GetAllocations();
                var fixedSampledAllocations = upscaler.GetAllocs().ToList();

                Console.WriteLine($"   Poisson {meanPoisson} KB sampling");
                var poissonSampler = new PoissonSampler(meanPoisson);
                var poissonUpscaler = new PoissonUpscaler(meanPoisson);
                Engine poissonEngine = new Engine(provider, poissonSampler, poissonUpscaler);
                poissonEngine.Run();
                var poissonSampledAllocations = poissonUpscaler.GetAllocs().ToList();

                Console.WriteLine("---------------------------------------------");
                foreach (var realAllocation in realAllocations.OrderBy(alloc => { return alloc.Size; }))
                {
                    var realKey = $"{realAllocation.Type}+{realAllocation.Key}";              // V--- use realKey when key is used
                    Console.WriteLine($"{realAllocation.Count,9} | {realAllocation.Size,13} - {realAllocation.Type}");

                    float poissonCountRatio = 0;
                    float poissonSizeRatio = 0;
                    float countRatio = 0;
                    float sizeRatio = 0;

                    var poissonSampled = poissonSampledAllocations.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (poissonSampled != null)
                    {
                        poissonCountRatio = -(float)(realAllocation.Count - poissonSampled.Count) / (float)realAllocation.Count;
                        poissonSizeRatio = -(float)(realAllocation.Size - poissonSampled.Size) / (float)realAllocation.Size;
                    }

                    var sampled = fixedSampledAllocations.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (sampled != null)
                    {
                        countRatio = -(float)(realAllocation.Count - sampled.Count) / (float)realAllocation.Count;
                        sizeRatio = -(float)(realAllocation.Size - sampled.Size) / (float)realAllocation.Size;
                    }

                    // show Poisson sampling
                    if (poissonSampled != null)
                    {
                        if ((sampled == null) | (Math.Abs(poissonSizeRatio) < Math.Abs(sizeRatio)))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                        }

                        Console.WriteLine($"{poissonSampled.Count,9} | {poissonSampled.Size,13}  Poisson");
                        Console.WriteLine($"{poissonCountRatio,9:P1} | {poissonSizeRatio,13:P1}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |-----Poisson-----^");
                    }

                    // show fixed sampling
                    if (sampled != null)
                    {
                        if ((poissonSampled == null) | (Math.Abs(sizeRatio) < Math.Abs(poissonSizeRatio)))
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                        }

                        Console.WriteLine($"{sampled.Count,9} | {sampled.Size,13}  Fixed");
                        Console.WriteLine($"{countRatio,9:P1} | {sizeRatio,13:P1}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |------Fixed------^");
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }
            catch (Exception x)
            {
                Console.WriteLine($"Error in {allocFile}: {x.Message}");
            }
        }

        private static void ParseCommandLine(string[] args, out string allocFile, out string allocDirectory, out int meanPoisson)
        {
            allocFile = string.Empty;
            allocDirectory = string.Empty;
            meanPoisson = 512;  // 512 KB is the mean of the distribution for Java

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if ("-m".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    if (i < args.Length)
                    {
                        if (!int.TryParse(args[i], out meanPoisson))
                        {
                            Console.WriteLine($"Invalid mean Poisson = {args[i]}");
                            meanPoisson = 512;
                        }
                    }
                }
                else
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
