using System;
using System.Collections.Generic;
using AutoMapper;
using Datadog.Trace.ClrProfiler;

namespace AutomapperTest
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine($"Profiler attached: {Instrumentation.ProfilerAttached}");

            Mapper.Initialize(
                configuration =>
                {
                    configuration.CreateMap<Model1, Model2>();
                });

            Console.WriteLine("Done");
        }
    }

    public class Model1
    {
        public List<string> Items { get; set; }
    }

    public class Model2
    {
        // changing this to string[] avoids the problem
        public List<string> Items { get; set; }
    }
}
