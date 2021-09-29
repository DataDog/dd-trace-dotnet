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
            Console.WriteLine($"Profiler attached: {IsProfilerAttached()}");

            Mapper.Initialize(
                configuration =>
                {
                    configuration.CreateMap<Model1, Model2>();
                });

            Console.WriteLine("Done");
        }

        private static bool IsProfilerAttached()
        {
            Type nativeMethodsType = Type.GetType("Datadog.Trace.ClrProfiler.NativeMethods, Datadog.Trace");
            MethodInfo profilerAttachedMethodInfo = nativeMethodsType.GetMethod("IsProfilerAttached");
            try
            {
                return (bool)profilerAttachedMethodInfo.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
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
