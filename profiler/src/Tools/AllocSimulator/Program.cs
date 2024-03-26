// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.

namespace AllocSimulator
{
    public class Program
    {
        private enum SamplingMode
        {
            None = 0,
            Fixed = 1,
            Poisson = 2,
            PoissonWithAllocationContext = 3
        }

        private enum UpscalingMode
        {
            None = 0,
            Fixed = 1,
            Poisson = 2,
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
                    out SamplingMode sampling2,
                    out UpscalingMode upscalingMode);

                if (string.IsNullOrEmpty(allocDirectory))
                {
                    if (string.IsNullOrEmpty(allocFile))
                    {
                        throw new InvalidOperationException("Missing allocations file...");
                    }

                    var extension = Path.GetExtension(allocFile);
                    if ((extension == ".alloc") || (extension == ".balloc"))
                    {
                        SimulateAllocations(allocFile, meanPoisson, sampling1, sampling2);
                        return;
                    }

                    if (extension == ".pprof")
                    {
                        DumpProfile(allocFile, upscalingMode, meanPoisson);
                        return;
                    }
                }

                foreach (var filename in Directory.GetFiles(allocDirectory, "*.alloc"))
                {
                    SimulateAllocations(filename, meanPoisson, sampling1, sampling2);
                }

                foreach (var filename in Directory.GetFiles(allocDirectory, "*.balloc"))
                {
                    SimulateAllocations(filename, meanPoisson, sampling1, sampling2);
                }
            }
            catch (InvalidOperationException x)
            {
                Console.WriteLine("AllocSimulator <allocations recording file .balloc or .alloc> [-m Poisson mean] [-d directory] [-c x y with 1, 2, or 3 for x or y] [-u x with 1, 2, or 3 for x]");
                Console.WriteLine("   -u is supported only when comparing .pprof and .balloc");
                Console.WriteLine("   for -c and -u, 1 = Fixed 100 KB threshold");
                Console.WriteLine("                  2 = Poisson");
                Console.WriteLine("                  3 = Poisson after 8 KB allocation context");
                Console.WriteLine("----------------------------------------------------");
                Console.WriteLine($"Error: {x.Message}");
            }
        }

        // if both .balloc and .pprof files exist, compare them
        // else if only .pprof exist, dump the allocations (should be sampled)
        // if upcaling mode is provided, compute the upscaled value
        private static void DumpProfile(string filename, UpscalingMode upscalingMode, int meanPoisson)
        {
            try
            {
                // get allocations from the .pprof
                var profile = ProfileAllocations.Load(filename);
                var sampledAllocations = profile.GetAllocations().ToList();
                var totalSampledBytes = sampledAllocations.Sum(alloc => alloc.Size);

                // get the recorded allocations if any
#pragma warning disable CS8604 // Possible null reference argument.
                var recordedAllocationsFile = Path.Combine(
                    Path.GetDirectoryName(filename),
                    Path.GetFileNameWithoutExtension(filename)) + ".balloc";
#pragma warning restore CS8604 // Possible null reference argument.

                // just dump the sampleds allocations if no recording
                if (!File.Exists(recordedAllocationsFile))
                {
                    Console.WriteLine($"Dumping allocations from {filename}");
                    foreach (var allocation in sampledAllocations.OrderBy(alloc => alloc.Size))
                    {
                        Console.WriteLine($"{allocation.Count,8} | {allocation.Size,12} - {allocation.Type}");
                    }

                    return;
                }

                Console.WriteLine($"Comparing allocations between {filename} and {recordedAllocationsFile}");
                Console.WriteLine("---------------------------------------------");
                IUpscaler upscaler = null;
                if (upscalingMode == UpscalingMode.Poisson)
                {
                    upscaler = new PoissonUpscaler(meanPoisson);
                }

                IAllocProvider provider = new BinaryFileAllocProvider(recordedAllocationsFile);
                var realAllocations = AggregateAllocations(provider.GetAllocations()).OrderBy(alloc => alloc.Size).ToList();
                var totalAllocatedBytes = realAllocations.Sum(alloc => alloc.Size);
                foreach (var realAllocation in realAllocations)
                {
                    Console.WriteLine($"{realAllocation.Count,9} | {realAllocation.Size,13} - {realAllocation.Type}");

                    float countRatio = 0;
                    float sizeRatio = 0;

                    var sampled = sampledAllocations.FirstOrDefault(a => (a.Type.EndsWith(realAllocation.Type)));
                    if (sampled != null)
                    {
                        Console.WriteLine($"{sampled.Count,9} | {sampled.Size,13}");

                        AllocInfo upscaled = new AllocInfo();
                        if (upscaler != null)
                        {
                            // Poisson
                            upscaler.Upscale(sampled, ref upscaled);
                        }
                        else if (upscalingMode == UpscalingMode.Fixed)
                        {
                            // Fixed
                            upscaled.Size = (long)((sampled.Size * totalAllocatedBytes) / totalSampledBytes);
                            upscaled.Count = (int)((sampled.Count * totalAllocatedBytes) / totalSampledBytes);
                        }
                        else
                        {
                            // none
                            // nothing to upscale
                            upscaled.Size = sampled.Size;
                            upscaled.Count = sampled.Count;
                        }

                        countRatio = -(float)(realAllocation.Count - upscaled.Count) / (float)realAllocation.Count;
                        sizeRatio = -(float)(realAllocation.Size - upscaled.Size) / (float)realAllocation.Size;
                        Console.WriteLine($"{upscaled.Count,9} | {upscaled.Size,13}  {upscalingMode.ToString()}");
                        Console.WriteLine($"{countRatio,9:P1} | {sizeRatio,13:P1}");
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |-----------------^");
                    }

                    Console.WriteLine();
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception x)
            {
                throw new InvalidOperationException(x.Message);
            }
        }

        private static IEnumerable<AllocInfo> AggregateAllocations(IEnumerable<AllocInfo> allocations)
        {
            Dictionary<string, AllocInfo> perTypeAllocations = new Dictionary<string, AllocInfo>(256);

            foreach (var alloc in allocations)
            {
                var type = alloc.Type;
                if (!perTypeAllocations.TryGetValue(type, out var info))
                {
                    info = new AllocInfo()
                    {
                        Type = type,
                        Size = 0,
                        Count = 0
                    };

                    perTypeAllocations[type] = info;
                }

                info.Size += alloc.Size;
                info.Count += 1;
            }

            return perTypeAllocations.Values;
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
                var realAllocations = engine1.GetAllocations();
                var sampledAllocations1 = upscaler1.GetSampledAllocs().ToList();
                var upscaledAllocations1 = upscaler1.GetUpscaledAllocs().ToList();

                Console.WriteLine($"   {sampler2.GetDescription()}");
                Engine engine2 = new Engine(provider, sampler2, upscaler2);
                engine2.Run();
                var upscaledAllocations2 = upscaler2.GetUpscaledAllocs().ToList();
                var sampledAllocations2 = upscaler2.GetSampledAllocs().ToList();

                Console.WriteLine("---------------------------------------------");
                foreach (var realAllocation1 in realAllocations.OrderBy(alloc => { return alloc.Size; }))
                {
                    var realKey = $"{realAllocation1.Type}+{realAllocation1.Key}";              // V--- use realKey when key is used
                    Console.WriteLine($"{realAllocation1.Count,9} | {realAllocation1.Size,13} - {realAllocation1.Type}");

                    float countRatio1 = 0;
                    float sizeRatio1 = 0;
                    float countRatio2 = 0;
                    float sizeRatio2 = 0;

                    var upscaled2 = upscaledAllocations2.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    var sampled2 = sampledAllocations2.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (upscaled2 != null)
                    {
                        countRatio2 = -(float)(realAllocation1.Count - upscaled2.Count) / (float)realAllocation1.Count;
                        sizeRatio2 = -(float)(realAllocation1.Size - upscaled2.Size) / (float)realAllocation1.Size;
                    }

                    var upscaled1 = upscaledAllocations1.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    var sampled1 = sampledAllocations1.FirstOrDefault(a => (realKey == $"{a.Type}+{a.Key}"));
                    if (upscaled1 != null)
                    {
                        countRatio1 = -(float)(realAllocation1.Count - upscaled1.Count) / (float)realAllocation1.Count;
                        sizeRatio1 = -(float)(realAllocation1.Size - upscaled1.Size) / (float)realAllocation1.Size;
                    }

                    if (upscaled1 != null)
                    {
                        if ((upscaled2 == null) | (Math.Abs(sizeRatio1) < Math.Abs(sizeRatio2)))
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        Console.WriteLine($"{sampled1.Count,9} | {sampled1.Size,13}  {sampler1.GetName()}");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Console.WriteLine($"{upscaled1.Count,9} | {upscaled1.Size,13}  {sampler1.GetName()}");
                        Console.WriteLine($"{countRatio1,9:P1} | {sizeRatio1,13:P1}");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    else
                    {
                        Console.WriteLine($"        ~ |------{sampler1.GetName()}------^");
                    }

                    if (upscaled2 != null)
                    {
                        if ((upscaled1 == null) | (Math.Abs(sizeRatio2) < Math.Abs(sizeRatio1)))
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                        }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        Console.WriteLine($"{sampled2.Count,9} | {sampled2.Size,13}  {sampler2.GetName()}");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Console.WriteLine($"{upscaled2.Count,9} | {upscaled2.Size,13}  {sampler2.GetName()}");
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
            out SamplingMode sampling2,
            out UpscalingMode upscalingMode)
        {
            allocFile = string.Empty;
            allocDirectory = string.Empty;
            meanPoisson = 100;  // 100 KB is the mean of the distribution for AllocationTick in .NET
            sampling1 = SamplingMode.Fixed;
            sampling2 = SamplingMode.PoissonWithAllocationContext;
            upscalingMode = UpscalingMode.None;

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
                        if ((val > 3) || (val < 0))
                        {
                            throw new InvalidOperationException($"Invalid comparison = {args[i]} (must be 0, 1, 2 or 3)");
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

            UpscalingMode GetUpscalingMode(string[] args, int i)
            {
                if (i < args.Length)
                {
                    if (!int.TryParse(args[i], out int val))
                    {
                        throw new InvalidOperationException($"Invalid comparison = {args[i]}");
                    }
                    else
                    {
                        if ((val > 2) || (val < 0))
                        {
                            throw new InvalidOperationException($"Invalid comparison = {args[i]} (must be 0, 1 or 2)");
                        }
                        else
                        {
                            return (UpscalingMode)val;
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Missing upscaling mode on the command line...");
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
                if ("-u".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    // should provide the upscaling mode
                    //  1 = fixed threshold
                    //  2 = Poisson threshold
                    //  3 = Poisson threshold after allocation context
                    //
                    i++;
                    upscalingMode = GetUpscalingMode(args, i);
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

#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
