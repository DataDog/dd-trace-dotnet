// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace DynamicAllocationSampling
{
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1407 // Arithmetic expressions should declare precedence
#pragma warning disable IDE0007 // Use implicit type

    public class Program
    {
        private const long SAMPLING_MEAN = 100 * 1024;
        private const double SAMPLING_RATIO = 0.999990234375 / 0.000009765625;

        private static Dictionary<string, TypeInfo> _sampledTypes = new Dictionary<string, TypeInfo>();
        private static List<Dictionary<string, TypeInfo>> _sampledTypesInRun = null;
        private static int _allocationsCount = 0;
        private static List<string> _allocatedTypes = new List<string>();
        private static EventPipeEventSource _source;

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("No process ID specified");
                return;
            }

            int pid = -1;
            if (!int.TryParse(args[0], out pid))
            {
                Console.WriteLine($"Invalid specified process ID '{args[0]}'");
                return;
            }

            try
            {
                PrintEventsLive(pid);
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
            }
        }

        public static void PrintEventsLive(int processId)
        {
            var providers = new List<EventPipeProvider>()
            {
                new EventPipeProvider(
                        "Microsoft-Windows-DotNETRuntime",
                        EventLevel.Verbose, // verbose is required for AllocationTick
                        (long)0x80000000001), // new AllocationSamplingKeyword + GCKeyword
                new EventPipeProvider(
                        "Allocations-Run",
                        EventLevel.Informational),
            };
            var client = new DiagnosticsClient(processId);

            using (var session = client.StartEventPipeSession(providers, false))
            {
                Console.WriteLine();

                Task streamTask = Task.Run(() =>
                {
                    var source = new EventPipeEventSource(session.EventStream);
                    _source = source;

                    ClrTraceEventParser clrParser = new ClrTraceEventParser(source);
                    source.Dynamic.All += OnEvents;

                    try
                    {
                        source.Process();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error encountered while processing events: {e.Message}");
                    }
                });

                Task inputTask = Task.Run(() =>
                {
                    while (Console.ReadKey().Key != ConsoleKey.Enter)
                    {
                        Thread.Sleep(100);
                    }

                    session.Stop();
                });

                Task.WaitAny(streamTask, inputTask);
            }

            // not all cases are emitting allocations run events
            if ((_sampledTypesInRun == null) && (_sampledTypes.Count > 0))
            {
                ShowIterationResults();
            }
        }

        private static long UpscaleSize(long totalSize, int count, long mean, long sizeRemainder)
        {
            // use the upscaling method detailed in the PR
            // = sq/p + u
            //   s = # of samples for a type
            //   q = 1 - 1/102400
            //   p = 1/102400
            //   u = sum of object remainders = Sum(object_size - sampledByteOffset) for all samples
            return (long)(SAMPLING_RATIO * count + sizeRemainder);
        }

        private static long UpscalePoissonSize(long totalSize, int count, long mean)
        {
            // This is the Poisson process based scaling
            var averageSize = (double)totalSize / (double)count;
            var scale = 1 / (1 - Math.Exp(-averageSize / mean));
            return (long)(totalSize * scale);
        }

        private static void OnEvents(TraceEvent eventData)
        {
            if (eventData.ID == (TraceEventID)303)
            {
                AllocationSampledData payload = new AllocationSampledData(eventData, _source.PointerSize);

                // skip unexpected types
                if (!_allocatedTypes.Contains(payload.TypeName))
                {
                    return;
                }

                if (!_sampledTypes.TryGetValue(payload.TypeName + payload.ObjectSize, out TypeInfo typeInfo))
                {
                    typeInfo = new TypeInfo() { TypeName = payload.TypeName, Count = 0, Size = (int)payload.ObjectSize, TotalSize = 0, RemainderSize = payload.ObjectSize - payload.SampledByteOffset };
                    _sampledTypes.Add(payload.TypeName + payload.ObjectSize, typeInfo);
                }

                typeInfo.Count++;
                typeInfo.TotalSize += (int)payload.ObjectSize;
                typeInfo.RemainderSize += (payload.ObjectSize - payload.SampledByteOffset);

                return;
            }

            if (eventData.ID == (TraceEventID)600)
            {
                AllocationsRunData payload = new AllocationsRunData(eventData);
                Console.WriteLine($"> starts {payload.Iterations} iterations allocating {payload.Count} instances");

                _sampledTypesInRun = new List<Dictionary<string, TypeInfo>>(payload.Iterations);
                _allocationsCount = payload.Count;
                string allocatedTypes = payload.AllocatedTypes;
                if (allocatedTypes.Length > 0)
                {
                    _allocatedTypes = allocatedTypes.Split(';').ToList();
                }

                return;
            }

            if (eventData.ID == (TraceEventID)601)
            {
                Console.WriteLine("\n< run stops\n");

                ShowRunResults();
                return;
            }

            if (eventData.ID == (TraceEventID)602)
            {
                AllocationsRunIterationData payload = new AllocationsRunIterationData(eventData);
                Console.Write($"{payload.Iteration}");

                _sampledTypes.Clear();
                return;
            }

            if (eventData.ID == (TraceEventID)603)
            {
                Console.WriteLine("|");
                ShowIterationResults();

                _sampledTypesInRun.Add(_sampledTypes);
                _sampledTypes = new Dictionary<string, TypeInfo>();
                return;
            }
        }

        private static void ShowRunResults()
        {
            var iterations = _sampledTypesInRun.Count;

            // for each type, get the percent diff between upscaled count and expected _allocationsCount
            Dictionary<TypeInfo, List<(double UpscaledCount, double PoissonCount)>> typeDistribution = new Dictionary<TypeInfo, List<(double UpscaledCount, double PoissonCount)>>();
            foreach (var iteration in _sampledTypesInRun)
            {
                foreach (var info in iteration.Values)
                {
                    // ignore types outside of the allocations run
                    if (info.Count < 1)
                    {
                        continue;
                    }

                    if (!typeDistribution.TryGetValue(info, out List<(double UpscaledCount, double PoissonCount)> distribution))
                    {
                        distribution = new List<(double UpscaledCount, double PoissonCount)>(iterations);
                        typeDistribution.Add(info, distribution);
                    }

                    var upscaledSize = UpscaleSize(info.TotalSize, info.Count, SAMPLING_MEAN, info.RemainderSize);
                    var upscaledCount = (long)info.Count * upscaledSize / info.TotalSize;
                    var poissonSize = UpscalePoissonSize(info.TotalSize, info.Count, SAMPLING_MEAN);
                    var poissonCount = (long)info.Count * poissonSize / info.TotalSize;
                    (double UpscaledCount, double PoissonCount) stats = (
                        (double)(upscaledCount - _allocationsCount) / (double)_allocationsCount,
                        (double)(poissonCount - _allocationsCount) / (double)_allocationsCount);
                    distribution.Add(stats);
                }
            }

            foreach (var type in typeDistribution.Keys.OrderBy(t => t.Size))
            {
                var distribution = typeDistribution[type];

                string typeName = type.TypeName;
                if (typeName.Contains("[]"))
                {
                    typeName += $" ({type.Size} bytes)";
                }

                Console.WriteLine(typeName);
                Console.WriteLine("-------------------------");
                int current = 1;
                foreach (var diff in distribution.OrderBy(v => v.UpscaledCount))
                {
                    if (iterations > 20)
                    {
                        if ((current <= 5) || ((current >= 49) && (current < 52)) || (current >= 96))
                        {
                            Console.WriteLine($"{current,4} {diff.UpscaledCount,8:0.0 %} | {diff.PoissonCount,8:0.0 %}");
                        }
                        else
                        if ((current == 6) || (current == 95))
                        {
                            Console.WriteLine("        ...");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"{current,4} {diff.UpscaledCount,8:0.0 %} | {diff.PoissonCount,8:0.0 %}");
                    }

                    current++;
                }

                Console.WriteLine();
            }
        }

        private static void ShowIterationResults()
        {
            // NOTE: need to take the size into account for array types
            // print the sampled types for both AllocationTick and AllocationSampled
            // TODO: remove AllocationTick data
            Console.WriteLine("  SCount          SSize   UnitSize     UpscaledSize     PoissonSize  UpscaledCount  PoissonCount  Name");
            Console.WriteLine("-----------------------------------------------------------------------------------------------------------------------------");
            foreach (var type in _sampledTypes.Values.OrderBy(v => v.Size))
            {
                Console.Write($"  {type.Count,6}");
                Console.Write($"  {type.TotalSize,13}");

                string typeName = type.TypeName;
                if (typeName.Contains("[]"))
                {
                    typeName += $" ({type.Size} bytes)";
                }

                if (type.Count != 0)
                {
                    Console.WriteLine($"  {type.TotalSize / type.Count,9}    {UpscaleSize(type.TotalSize, type.Count, SAMPLING_MEAN, type.RemainderSize),13}   {UpscalePoissonSize(type.TotalSize, type.Count, SAMPLING_MEAN),13}     {(long)type.Count * UpscaleSize(type.TotalSize, type.Count, SAMPLING_MEAN, type.RemainderSize) / type.TotalSize,10}    {(long)type.Count * UpscalePoissonSize(type.TotalSize, type.Count, SAMPLING_MEAN) / type.TotalSize,10}  {typeName}");
                }
            }
        }
    }

    internal class TypeInfo
    {
        public string TypeName { get; set; } = "?";
        public int Count { get; set; }
        public long Size { get; set; }
        public long TotalSize { get; set; }
        public long RemainderSize { get; set; }

        public override int GetHashCode()
        {
            return (TypeName + Size).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (!(obj is TypeInfo))
            {
                return false;
            }

            return (TypeName + Size).Equals(((TypeInfo)obj).TypeName + Size);
        }
    }

    // <data name="AllocationKind" inType="win:UInt32" map="GCAllocationKindMap" />
    // <data name="ClrInstanceID" inType="win:UInt16" />
    // <data name="TypeID" inType="win:Pointer" />
    // <data name="TypeName" inType="win:UnicodeString" />
    // <data name="Address" inType="win:Pointer" />
    // <data name="ObjectSize" inType="win:UInt64" outType="win:HexInt64" />
    // <data name="SampledByteOffset" inType="win:UInt64" outType="win:HexInt64" />
    internal class AllocationSampledData
    {
        private const int EndOfStringCharLength = 2;

        private TraceEvent _payload;
        private int _pointerSize;

        public AllocationSampledData(TraceEvent payload, int pointerSize)
        {
            _payload = payload;
            _pointerSize = pointerSize;
            TypeName = "?";

            ComputeFields();
        }

        public GCAllocationKind AllocationKind { get; set; }
        public int ClrInstanceID { get; set; }
        public UInt64 TypeID { get; set; }
        public string TypeName { get; set; }
        public UInt64 Address { get; set; }
        public long ObjectSize { get; set; }
        public long SampledByteOffset { get; set; }

        private void ComputeFields()
        {
            int offsetBeforeString = 4 + 2 + _pointerSize;

            Span<byte> data = _payload.EventData().AsSpan();
            AllocationKind = (GCAllocationKind)BitConverter.ToInt32(data.Slice(0, 4));
            ClrInstanceID = BitConverter.ToInt16(data.Slice(4, 2));
            if (_pointerSize == 4)
            {
                TypeID = BitConverter.ToUInt32(data.Slice(6, _pointerSize));
            }
            else
            {
                TypeID = BitConverter.ToUInt64(data.Slice(6, _pointerSize));
            }

            // \0 should not be included for GetString to work
            TypeName = Encoding.Unicode.GetString(data.Slice(offsetBeforeString, _payload.EventDataLength - offsetBeforeString - EndOfStringCharLength - _pointerSize - 8 - 8));
            if (_pointerSize == 4)
            {
                Address = BitConverter.ToUInt32(data.Slice(offsetBeforeString + TypeName.Length * 2 + EndOfStringCharLength, _pointerSize));
            }
            else
            {
                Address = BitConverter.ToUInt64(data.Slice(offsetBeforeString + TypeName.Length * 2 + EndOfStringCharLength, _pointerSize));
            }

            ObjectSize = BitConverter.ToInt64(data.Slice(offsetBeforeString + TypeName.Length * 2 + EndOfStringCharLength + _pointerSize, 8));
            SampledByteOffset = BitConverter.ToInt64(data.Slice(offsetBeforeString + TypeName.Length * 2 + EndOfStringCharLength + _pointerSize + 8, 8));
        }
    }

    internal class AllocationsRunData
    {
        private const int EndOfStringCharLength = 2;
        private TraceEvent _payload;

        public AllocationsRunData(TraceEvent payload)
        {
            _payload = payload;

            ComputeFields();
        }

        public int Iterations { get; set; }
        public int Count { get; set; }
        public string AllocatedTypes { get; set; }

        private void ComputeFields()
        {
            int offsetBeforeString = 4 + 4;

            Span<byte> data = _payload.EventData().AsSpan();
            Iterations = BitConverter.ToInt32(data.Slice(0, 4));
            Count = BitConverter.ToInt32(data.Slice(4, 4));
            AllocatedTypes = Encoding.Unicode.GetString(data.Slice(offsetBeforeString, _payload.EventDataLength - offsetBeforeString - EndOfStringCharLength));
        }
    }

    internal class AllocationsRunIterationData
    {
        private TraceEvent _payload;

        public AllocationsRunIterationData(TraceEvent payload)
        {
            _payload = payload;

            ComputeFields();
        }

        public int Iteration { get; set; }

        private void ComputeFields()
        {
            Span<byte> data = _payload.EventData().AsSpan();
            Iteration = BitConverter.ToInt32(data.Slice(0, 4));
        }
    }

#pragma warning restore IDE0007 // Use implicit type
#pragma warning restore SA1407 // Arithmetic expressions should declare precedence
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1402 // File may only contain a single type
}
