using System.Runtime.InteropServices;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Datadog.Trace.Tools.AotProcessor.Runtime;

namespace Datadog.Trace.Tools.AotProcessor;

internal class ProfilerInterop
{
    internal static unsafe NativeObjects.ICorProfilerCallback4Invoker? LoadProfiler(Rewriter rewriter)
    {
        var clsid = new Guid("846F5F1C-F9AE-4B07-969E-05C26BC060D8");
        var unknownIid = Interfaces.IUnknown.Guid; // "IUnknown
        var classFactoryIid = Interfaces.IClassFactory.Guid; // "IClassFactory
        IntPtr pcf;
        var hr = (HResult)Windows.DllGetClassObject(ref clsid, ref classFactoryIid, &pcf);
        if (hr.Failed) { return null; }
        var classFactory = new NativeObjects.IClassFactoryInvoker(pcf);

        IntPtr cpc;
        hr = classFactory.CreateInstance(IntPtr.Zero, unknownIid, out cpc);
        if (hr.Failed) { return null; }
        var profilerCallback = new NativeObjects.ICorProfilerCallback4Invoker(cpc);

        using var ptr = NativeObjects.IUnknown.Wrap(rewriter);
        hr = profilerCallback.Initialize(ptr);
        if (hr.Failed) { return null; }

        Console.WriteLine($"Profiler loaded successfully. IsProfilerAttached: {Windows.IsProfilerAttached()}");

        return profilerCallback;
    }

    private static unsafe class Windows
    {
        [DllImport("Datadog.Tracer.Native.dll")]
        public static extern int DllGetClassObject(ref Guid rclsid, ref Guid riid, IntPtr* ppv);

        [DllImport("Datadog.Tracer.Native.dll")]
        public static extern bool IsProfilerAttached();
    }
}
