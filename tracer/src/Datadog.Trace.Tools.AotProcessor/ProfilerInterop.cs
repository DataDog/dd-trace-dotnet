using System;
using System.Runtime.InteropServices;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor;

internal class ProfilerInterop
{
    private static Rewriter rewriter = new Rewriter();
    private static NativeObjects.ICorProfilerCallback4Invoker profilerCallback;
    private static int moduleId = 1;

    internal static unsafe bool LoadProfiler()
    {
        var clsid = new Guid("846F5F1C-F9AE-4B07-969E-05C26BC060D8");
        var unknownIid = Interfaces.IUnknown.Guid; // "IUnknown
        var classFactoryIid = Interfaces.IClassFactory.Guid; // "IClassFactory
        IntPtr pcf;
        var hr = (HResult)Windows.DllGetClassObject(ref clsid, ref classFactoryIid, &pcf);
        if (hr.Failed) { return false; }
        var classFactory = new NativeObjects.IClassFactoryInvoker(pcf);

        IntPtr cpc;
        hr = classFactory.CreateInstance(IntPtr.Zero, unknownIid, out cpc);
        if (hr.Failed) { return false; }
        profilerCallback = new NativeObjects.ICorProfilerCallback4Invoker(cpc);

        using var ptr = NativeObjects.IUnknown.Wrap(rewriter);
        hr = profilerCallback.Initialize(ptr);
        if (hr.Failed) { return false; }

        Console.WriteLine($"Profiler loaded successfully. IsProfilerAttached: {Windows.IsProfilerAttached()}");

        // Init Instrumentation -> We must tell the instrumenter is AOT, so we can provide what profiler provides by auto instrumentation in runtime
        Instrumentation.Initialize(true);

        return true;
    }

    internal static unsafe bool ProcessAssembly(string path)
    {
        // Read assembly with mono cecil
        var assembly = AssemblyDefinition.ReadAssembly(path);

        // Load module
        var mId = new ModuleId(moduleId);
        profilerCallback.ModuleLoadStarted(mId);

        // Process all methods in the assembly
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                // JIT Instrument method
            }
        }

        // Write processed assembly
        assembly.Write(path);
        return true;
    }

    private static unsafe class Windows
    {
        [DllImport("Datadog.Tracer.Native.dll")]
        public static extern int DllGetClassObject(ref Guid rclsid, ref Guid riid, IntPtr* ppv);

        [DllImport("Datadog.Tracer.Native.dll")]
        public static extern bool IsProfilerAttached();
    }
}
