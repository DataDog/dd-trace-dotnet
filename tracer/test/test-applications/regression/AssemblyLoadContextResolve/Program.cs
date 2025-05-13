using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace AssemblyLoadContextResolve;

internal class Program
{
    private static ConcurrentStack<string> _assemblyResolveCalls = new();

    private const string TestAssemblyName = "datadog_test_assembly";

    static void Main(string[] args)
    {
        AssemblyLoadContext.Default.Resolving += AssemblyResolving;

        var traceAssembly = Assembly.Load("Datadog.Trace");
        var alc = AssemblyLoadContext.GetLoadContext(traceAssembly);

        if (alc.GetType().FullName != "Datadog.Trace.ClrProfiler.Managed.Loader.ManagedProfilerAssemblyLoadContext")
        {
            throw new InvalidOperationException($"Datadog.Trace was loaded in the wrong ALC: {alc.GetType()}");
        }

        try
        {
            var testAssembly = Assembly.Load(TestAssemblyName);
            throw new InvalidOperationException($"Test assembly was found, this shouldn't happen: {testAssembly}");
        }
        catch (FileNotFoundException)
        {
            // Expected
        }

        var resolvedAssemblies = _assemblyResolveCalls.ToList();

        if (!resolvedAssemblies.Contains(TestAssemblyName))
        {
            throw new InvalidOperationException($"AssemblyResolving should have been called for {TestAssemblyName}: {string.Join(", ", resolvedAssemblies)}");
        }

        if (resolvedAssemblies.Contains("Datadog.Trace"))
        {
            throw new InvalidOperationException($"AssemblyResolving shouldn't have been called for Datadog.Trace: {string.Join(", ", resolvedAssemblies)}");
        }
    }

    private static Assembly AssemblyResolving(AssemblyLoadContext alc, AssemblyName assemblyname)
    {
        _assemblyResolveCalls.Push(assemblyname?.Name);
        return null;
    }
}
