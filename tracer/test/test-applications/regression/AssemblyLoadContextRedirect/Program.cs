using System;
using System.Reflection;

namespace AssemblyLoadContextRedirect
{
    /// <summary>
    /// This sample simulates an app that would redirect the assembly loading to a given folder.
    /// This will interfere with the managed loader, which will end up loading the nuget Datadog.Trace.dll
    /// instead of the one bundled with the profiler.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            var alc = new CustomAssemblyLoadContext();
            var assembly = alc.LoadFromAssemblyName(new AssemblyName(typeof(Program).Assembly.GetName().Name));

            var type = assembly.GetType("AssemblyLoadContextRedirect.StuffUsingTracer");
            type.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            Console.WriteLine("App completed successfully");
        }
    }
}
