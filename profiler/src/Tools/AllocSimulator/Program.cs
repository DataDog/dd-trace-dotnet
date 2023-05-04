// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

namespace AllocSimulator
{
    public class Program
    {
        private enum SamplingMode
        {
            Fixed = 1,
            Poisson = 2,
            PoissonWithAllocationContext = 3
        }

        public static void Main(string[] args)
        {
            try
            {
                ParseCommandLine(
                    args,
                    out string allocFile,
                    out string allocDirectory,
                    out int meanPoisson,
                    out SamplingMode sampling1,
                    out SamplingMode sampling2);

                if (string.IsNullOrEmpty(allocDirectory))
                {
                    SimulateAllocations(allocFile, meanPoisson, sampling1, sampling2);
                    return;
                }

                foreach (var filename in Directory.GetFiles(allocDirectory, "*.alloc"))
                {
                    SimulateAllocations(filename, meanPoisson, sampling1, sampling2);
                }
            }
            catch (InvalidOperationException x)
            {
                Console.WriteLine("AllocSimulator [-m Poisson mean] [-d directory] [-c x y with 1, 2, or 3 for x or y]");
                Console.WriteLine("   for -c, 1 = Fixed 100 KB threshold");
                Console.WriteLine("           2 = Poisson");
                Console.WriteLine("           3 = Poisson after 8 KB allocation context");
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine($"Error: {x.Message}");
            }
        }

        private static void GetSampler(SamplingMode samplingMode, out ISampler sampler, out IUpscaler upscaler, int meanPoisson, int allocationContextSize)
        {
            switch (samplingMode)
            {
                case SamplingMode.Fixed:
                {
                    sampler = new FixedSampler();
                    upscaler = new FixedUpscaler();
                }

                break;

                case SamplingMode.Poisson:
                {
                    sampler = new PoissonSampler(meanPoisson);
                    upscaler = new PoissonUpscaler(meanPoisson);
                }

                break;

                case SamplingMode.PoissonWithAllocationContext:
                {
                    sampler = new FixedAllocationContextSampler(meanPoisson, allocationContextSize);
                    upscaler = new PoissonUpscaler(meanPoisson);
                }

                break;

                default:
                throw new InvalidOperationException($"{(int)samplingMode}");
            }
        }

        private static void SimulateAllocations(string allocFile, int meanPoisson, SamplingMode sampling1, SamplingMode sampling2)
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

                GetSampler(sampling1, out ISampler sampler1, out IUpscaler upscaler1, meanPoisson, 8);
                GetSampler(sampling2, out ISampler sampler2, out IUpscaler upscaler2, meanPoisson, 8);

                Console.WriteLine($"   {sampler1.GetDescription()}");
                Engine engine1 = new Engine(provider, sampler1, upscaler1);
                engine1.Run();
                var realAllocations1 = engine1.GetAllocations();
                var sampledAllocations1 = upscaler1.GetAllocs().ToList();

                Console.WriteLine($"   {sampler2.GetDescription()}");
                Engine engine2 = new Engine(provider, sampler2, upscaler2);
                engine2.Run();
                var sampledAllocations2 = upscaler2.GetAllocs().ToList();

                Console.WriteLine("---------------------------------------------");
                foreach (var realAllocation1 in realAllocations1.OrderBy(alloc => { return alloc.Size; }))
                {
                    var realKey = $"{realAllocation1.Type}+{realAllocation1.Key}";              // V--- use realKey when key is used
                    Console.WriteLine($"{realAllocation1.Count,9} | {realAllocation1.Size,13} - {realAllocation1.Type}");

                    float countRatio1 = 0;
                    float sizeRatio1 = 0;
                    float countRatio2 = 0;
                    float sizeRatio2 = 0;

                    var sampled2 = sampledAllocations2.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (sampled2 != null)
                    {
                        countRatio2 = -(float)(realAllocation1.Count - sampled2.Count) / (float)realAllocation1.Count;
                        sizeRatio2 = -(float)(realAllocation1.Size - sampled2.Size) / (float)realAllocation1.Size;
                    }

                    var sampled1 = sampledAllocations1.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (sampled1 != null)
                    {
                        countRatio1 = -(float)(realAllocation1.Count - sampled1.Count) / (float)realAllocation1.Count;
                        sizeRatio1 = -(float)(realAllocation1.Size - sampled1.Size) / (float)realAllocation1.Size;
                    }

                    if (sampled1 != null)
                    {
                        if ((sampled2 == null) | (Math.Abs(sizeRatio1) < Math.Abs(sizeRatio2)))
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                        }

                        Console.WriteLine($"{sampled1.Count,9} | {sampled1.Size,13}  {sampler1.GetName()}");
                        Console.WriteLine($"{countRatio1,9:P1} | {sizeRatio1,13:P1}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |------{sampler1.GetName()}------^");
                    }

                    if (sampled2 != null)
                    {
                        if ((sampled1 == null) | (Math.Abs(sizeRatio2) < Math.Abs(sizeRatio1)))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                        }

                        Console.WriteLine($"{sampled2.Count,9} | {sampled2.Size,13}  {sampler2.GetName()}");
                        Console.WriteLine($"{countRatio2,9:P1} | {sizeRatio2,13:P1}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |-----{sampler2.GetName()}-----^");
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

        private static void ParseCommandLine(
            string[] args,
            out string allocFile,
            out string allocDirectory,
            out int meanPoisson,
            out SamplingMode sampling1,
            out SamplingMode sampling2)
        {
            allocFile = string.Empty;
            allocDirectory = string.Empty;
            meanPoisson = 512;  // 512 KB is the mean of the distribution for Java
            sampling1 = SamplingMode.Fixed;
            sampling2 = SamplingMode.PoissonWithAllocationContext;

            SamplingMode GetSamplingMode(string[] args, int i)
            {
                if (i < args.Length)
                {
                    if (!int.TryParse(args[i], out int val))
                    {
                        throw new InvalidOperationException($"Invalid comparison = {args[i]}");
                    }
                    else
                    {
                        if ((val > 3) || (val < 1))
                        {
                            throw new InvalidOperationException($"Invalid comparison = {args[i]} (must be 1, 2 or 3)");
                        }
                        else
                        {
                            return (SamplingMode)val;
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Missing sampling mode on the command line...");
                }
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if ("-c".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    // comparison should provide the 2 modes
                    //  1 = fixed threshold
                    //  2 = Poisson threshold
                    //  3 = Poisson threshold after allocation context
                    //
                    i++;
                    sampling1 = GetSamplingMode(args, i);

                    i++;
                    sampling2 = GetSamplingMode(args, i);
                }
                else
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
