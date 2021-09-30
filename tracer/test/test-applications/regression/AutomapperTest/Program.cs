using System;
using System.Collections.Generic;
using System.Reflection;
using AutoMapper;

namespace AutomapperTest
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine($"Profiler attached: {Samples.SampleHelpers.IsProfilerAttached()}");

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
