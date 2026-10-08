// <copyright file="NativeTracerHost.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Hosts the native tracer (Datadog.Tracer.Native) in this process and drives it through an emulated CLR so it
/// rewrites assemblies on disk exactly like it rewrites them at runtime.
/// </summary>
/// <remarks>
/// The native tracer keeps process-wide state, so a process hosts at most one instance (and one instrumentation run).
/// </remarks>
internal sealed unsafe class NativeTracerHost : IDisposable
{
    // The standard tracer CLSID: offline instrumentation uses the same profiler object as a runtime attach.
    private static readonly Guid TracerClsid = new("846F5F1C-F9AE-4B07-969E-05C26BC060D8");
    private static int _created;

    private readonly IntPtr _library;
    private readonly NativeObjects.ICorProfilerCallback4Invoker _profiler;
    private readonly EmulatedRuntime _runtime;
    private bool _shutdown;

    private NativeTracerHost(IntPtr library, NativeObjects.ICorProfilerCallback4Invoker profiler, EmulatedRuntime runtime)
    {
        _library = library;
        _profiler = profiler;
        _runtime = runtime;
    }

    public EmulatedRuntime Runtime => _runtime;

    public static NativeTracerHost Create(string nativeTracerPath, Version runtimeVersion)
    {
        if (Interlocked.Exchange(ref _created, 1) != 0)
        {
            throw new InvalidOperationException("The native tracer can only be hosted once per process.");
        }

        var library = NativeLibrary.Load(nativeTracerPath);
        var dllGetClassObject = (delegate* unmanaged<Guid*, Guid*, IntPtr*, int>)NativeLibrary.GetExport(library, "DllGetClassObject");
        var clsid = TracerClsid;
        var classFactoryIid = IClassFactory.Guid;
        IntPtr classFactoryPointer;
        Check(dllGetClassObject(&clsid, &classFactoryIid, &classFactoryPointer), "DllGetClassObject");
        var classFactory = new NativeObjects.IClassFactoryInvoker(classFactoryPointer);
        Check(classFactory.CreateInstance(IntPtr.Zero, IUnknown.Guid, out var profilerPointer), "IClassFactory.CreateInstance");
        var profiler = new NativeObjects.ICorProfilerCallback4Invoker(profilerPointer);

        var runtime = new EmulatedRuntime(runtimeVersion);
        Check(profiler.Initialize(runtime.InfoPointer), "ICorProfilerCallback.Initialize");
        AotLog.Debug($"Native tracer initialized. Event mask: {runtime.EventsLow} / {runtime.EventsHigh}");
        Check(profiler.AppDomainCreationStarted(new AppDomainId(1)), "AppDomainCreationStarted");
        Check(profiler.AppDomainCreationFinished(new AppDomainId(1), HResult.S_OK), "AppDomainCreationFinished");
        return new NativeTracerHost(library, profiler, runtime);
    }

    /// <summary>
    /// Enables the CallTarget definitions embedded in the native tracer for the given categories and target framework,
    /// like Instrumentation.Initialize does at runtime.
    /// </summary>
    public int EnableEmbeddedDefinitions(uint categories, uint targetFramework)
    {
        var init = (delegate* unmanaged<uint, uint, int>)NativeLibrary.GetExport(_library, "InitEmbeddedCallTargetDefinitions");
        return init(categories, targetFramework);
    }

    /// <summary>
    /// Enables the call site (IAST/RASP) aspects embedded in the native tracer for the given categories and target
    /// framework, like Instrumentation.EnableCallSiteInstrumentations does at runtime. Before the modules are loaded:
    /// the native tracer's dataflow follows the module loads.
    /// </summary>
    public int EnableEmbeddedCallSites(uint categories, uint targetFramework)
    {
        var init = (delegate* unmanaged<uint, uint, int>)NativeLibrary.GetExport(_library, "InitEmbeddedCallSiteDefinitions");
        return init(categories, targetFramework);
    }

    /// <summary>
    /// Runs the call site rewriting of every method of a module, which the JIT events trigger at runtime: the native
    /// tracer requests the ReJIT of the methods it rewrites (excluded assemblies, like the framework's, are skipped).
    /// </summary>
    /// <returns>The number of methods the native tracer processed.</returns>
    public int ProcessCallSites(ModuleState module)
    {
        if (!NativeLibrary.TryGetExport(_library, "ProcessCallSites", out var export))
        {
            throw new InvalidOperationException("The native tracer can't instrument call sites offline (no ProcessCallSites export): use a matching Datadog.Tracer.Native.");
        }

        var process = (delegate* unmanaged<nint, uint, int>)export;
        var processed = 0;
        foreach (var type in module.Module.GetTypes())
        {
            foreach (var method in type.Methods)
            {
                if (method.HasBody && process(module.Id, method.MDToken.Raw) != 0)
                {
                    processed++;
                }
            }
        }

        if (processed > 0)
        {
            AotLog.Debug($"{module.AssemblyName}: {processed} methods processed for call sites");
        }

        return processed;
    }

    /// <summary>
    /// Registers definitions by running a managed method that calls Datadog.Trace's NativeMethods (test applications
    /// such as CallTargetNativeTest inject their own definitions this way). The P/Invokes are bound to the hosted library.
    /// </summary>
    public void InvokeDefinitionsMethod(string datadogTracePath, string assemblyPath, string typeName, string methodName)
    {
        var datadogTrace = Assembly.LoadFrom(datadogTracePath);
        var library = _library;
        NativeLibrary.SetDllImportResolver(datadogTrace, (name, _, _) => name.StartsWith("Datadog.Tracer.Native", StringComparison.Ordinal) ? library : IntPtr.Zero);
        var assembly = Assembly.LoadFrom(assemblyPath);
        var type = assembly.GetType(typeName, throwOnError: true)!;
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                     ?? throw new InvalidOperationException($"Method {typeName}.{methodName} not found in {assemblyPath}");
        method.Invoke(null, null);
    }

    /// <summary>
    /// Loads a module through the emulated CLR, raising the same callbacks the CLR raises.
    /// </summary>
    public ModuleState LoadModule(string path, bool writable)
    {
        var state = _runtime.AddModule(path, writable);
        var assemblyId = new AssemblyId(state.AssemblyId);
        var moduleId = new ModuleId(state.Id);
        Check(_profiler.AssemblyLoadStarted(assemblyId), "AssemblyLoadStarted");
        Check(_profiler.ModuleLoadStarted(moduleId), "ModuleLoadStarted");
        Check(_profiler.ModuleLoadFinished(moduleId, HResult.S_OK), "ModuleLoadFinished");
        Check(_profiler.ModuleAttachedToAssembly(moduleId, assemblyId), "ModuleAttachedToAssembly");
        Check(_profiler.AssemblyLoadFinished(assemblyId, HResult.S_OK), "AssemblyLoadFinished");
        AotLog.Debug($"Loaded {state.Module.Name} (module {state.Id}, writable: {writable})");
        return state;
    }

    /// <summary>
    /// Loads, read-only, every assembly referenced by the loaded modules that is found in the probe directories, so
    /// the native tracer can resolve type references across modules (for example System.Runtime forwards).
    /// </summary>
    public void LoadReferenceClosure(IReadOnlyList<string> probeDirectories)
    {
        var loaded = new HashSet<string>(_runtime.Modules.Select(m => m.AssemblyName), StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<ModuleState>(_runtime.Modules);
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            foreach (var reference in current.Module.GetAssemblyRefs())
            {
                if (!loaded.Add(reference.Name))
                {
                    continue;
                }

                var candidate = probeDirectories.Select(d => Path.Combine(d, reference.Name + ".dll")).FirstOrDefault(File.Exists);
                if (candidate is null)
                {
                    AotLog.Debug($"Reference {reference.Name} not found in the probe directories");
                    continue;
                }

                pending.Enqueue(LoadModule(candidate, writable: false));
            }
        }
    }

    /// <summary>
    /// Runs the ReJIT requests the native tracer made, from this thread, like the CLR does before a method runs. The
    /// native tracer makes them from its ReJIT worker: a barrier (WaitForPendingRejitWork) tells when the work queued
    /// so far ran, and requests are processed until a barrier finds none left. A native tracer without the barrier is
    /// waited for until no request came for <paramref name="idleTimeout"/>.
    /// </summary>
    /// <returns>The number of methods processed.</returns>
    public int ProcessReJitRequests(TimeSpan idleTimeout)
    {
        if (!NativeLibrary.TryGetExport(_library, "WaitForPendingRejitWork", out var export))
        {
            AotLog.Warn("The native tracer has no ReJIT barrier (WaitForPendingRejitWork): waiting until it stays idle.");
            return ProcessReJitRequestsUntilIdle(idleTimeout);
        }

        var waitForPendingRejitWork = (delegate* unmanaged<uint, int>)export;
        var processed = 0;
        while (true)
        {
            if (waitForPendingRejitWork(120_000) == 0)
            {
                AotLog.Warn("The native tracer's ReJIT work didn't complete: waiting until it stays idle.");
                return processed + ProcessReJitRequestsUntilIdle(idleTimeout);
            }

            if (_runtime.PendingReJit.IsEmpty)
            {
                return processed;
            }

            while (_runtime.PendingReJit.TryDequeue(out var request))
            {
                ProcessReJitRequest(request);
                processed++;
            }
        }
    }

    private int ProcessReJitRequestsUntilIdle(TimeSpan idleTimeout)
    {
        var processed = 0;
        var idleSince = DateTime.UtcNow;
        while (DateTime.UtcNow - idleSince < idleTimeout)
        {
            if (!_runtime.PendingReJit.TryDequeue(out var request))
            {
                Thread.Sleep(20);
                continue;
            }

            ProcessReJitRequest(request);
            processed++;
            idleSince = DateTime.UtcNow;
        }

        return processed;
    }

    private void ProcessReJitRequest((int Module, uint MethodToken) request)
    {
        var functionId = new FunctionId(_runtime.FunctionIdFor(request.Module, request.MethodToken));
        var rejitId = new ReJITId(functionId.Value);
        var control = NativeObjectRoots.Add(NativeObjects.ICorProfilerFunctionControl.Wrap(new FunctionControl(_runtime, request.Module, request.MethodToken)));
        _profiler.ReJITCompilationStarted(functionId, rejitId, 1);
        var result = _profiler.GetReJITParameters(new ModuleId(request.Module), new MdMethodDef((int)request.MethodToken), control);
        if (result.Failed)
        {
            AotLog.Warn($"GetReJITParameters failed for module {request.Module} method 0x{request.MethodToken:x8}: {result}");
        }

        _profiler.ReJITCompilationFinished(functionId, rejitId, HResult.S_OK, 1);
    }

    public void Dispose()
    {
        if (!_shutdown)
        {
            _shutdown = true;
            _profiler.Shutdown();
        }

        _runtime.Dispose();
    }

    private static void Check(HResult hr, string operation)
    {
        if (hr.Failed)
        {
            throw new InvalidOperationException($"{operation} failed: {hr}");
        }
    }
}
#endif
