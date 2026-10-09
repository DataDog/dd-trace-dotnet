// <copyright file="EmulatedRuntime.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using dnlib.DotNet;
using dnlib.DotNet.MD;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

/// <summary>
/// Emulates the subset of ICorProfilerInfo the native tracer uses while rewriting modules offline.
/// </summary>
/// <remarks>
/// It implements ICorProfilerInfo12 because the native tracer requires it on ARM (where it means .NET 5+), but it only
/// exposes ICorProfilerInfo8 and ICorProfilerInfo12: ICorProfilerInfo10 would enable the runtime ReJIT path.
/// </remarks>
internal sealed unsafe class EmulatedRuntime : ICorProfilerInfo12, IDisposable
{
    private const int AppDomain = 1;

    private readonly Dictionary<int, ModuleState> _modules = new();
    private readonly Dictionary<int, ModuleMetadata> _metadata = new();
    private readonly Dictionary<nint, (int Module, uint MethodToken)> _functions = new();
    private readonly Dictionary<(int Module, uint MethodToken), nint> _functionIds = new();
    private readonly Dictionary<(int Module, uint MethodToken), IntPtr> _originalBodies = new();
    private readonly Dictionary<IntPtr, int> _allocations = new();
    private readonly object _sync = new();
    private readonly NativeObjects.ICorProfilerInfo8 _info;
    private readonly NativeObjects.ICorProfilerInfo12 _info12;
    private readonly NativeObjects.IMethodMalloc _malloc;
    private nint _nextFunctionId = 0x10000;
    private int _reJitRequests;

    public EmulatedRuntime(Version runtimeVersion)
    {
        RuntimeVersion = runtimeVersion;
        _info = NativeObjects.ICorProfilerInfo8.Wrap(this);
        _info12 = NativeObjects.ICorProfilerInfo12.Wrap(this);
        _malloc = NativeObjects.IMethodMalloc.Wrap(new MethodMalloc(this));
    }

    public Version RuntimeVersion { get; }

    public PinnedBlobs Blobs { get; } = new();

    public IntPtr InfoPointer => _info;

    public CorPrfMonitor EventsLow { get; private set; }

    public CorPrfHighMonitor EventsHigh { get; private set; }

    public ConcurrentQueue<(int Module, uint MethodToken)> PendingReJit { get; } = new();

    public int ReJitRequests => Volatile.Read(ref _reJitRequests);

    public IReadOnlyCollection<ModuleState> Modules
    {
        get
        {
            lock (_sync)
            {
                return _modules.Values.ToArray();
            }
        }
    }

    public void Dispose()
    {
        Blobs.Dispose();
        lock (_sync)
        {
            foreach (var pointer in _originalBodies.Values)
            {
                Marshal.FreeHGlobal(pointer);
            }

            _originalBodies.Clear();
            foreach (var module in _modules.Values)
            {
                module.Module.Dispose();
            }
        }
    }

    public ModuleState AddModule(string path, bool writable)
    {
        lock (_sync)
        {
            var id = _modules.Count + 1;
            var module = ModuleDefMD.Load(path, new ModuleCreationOptions { TryToLoadPdbFromDisk = true });
            var state = new ModuleState(id, path, module, writable);
            _modules[id] = state;
            _metadata[id] = new ModuleMetadata(this, state);
            return state;
        }
    }

    /// <summary>
    /// The scope one of the metadata interfaces handed to the native tracer belongs to.
    /// </summary>
    public ModuleMetadata? FindMetadata(IntPtr pointer)
    {
        lock (_sync)
        {
            return _metadata.Values.FirstOrDefault(m => m.Owns(pointer));
        }
    }

    public ModuleMetadata GetMetadata(int id)
    {
        lock (_sync)
        {
            return _metadata[id];
        }
    }

    public ModuleState GetModule(int id)
    {
        lock (_sync)
        {
            return _modules[id];
        }
    }

    public nint FunctionIdFor(int module, uint methodToken)
    {
        lock (_sync)
        {
            if (!_functionIds.TryGetValue((module, methodToken), out var id))
            {
                id = _nextFunctionId++;
                _functionIds[(module, methodToken)] = id;
                _functions[id] = (module, methodToken);
            }

            return id;
        }
    }

    internal IntPtr Allocate(uint size)
    {
        var pointer = Marshal.AllocHGlobal((int)Math.Max(1, size));
        lock (_sync)
        {
            _allocations[pointer] = (int)size;
        }

        return pointer;
    }

    internal void StoreNewBody(int module, uint methodToken, IntPtr body, int knownSize)
    {
        var size = knownSize;
        if (size <= 0)
        {
            lock (_sync)
            {
                _allocations.TryGetValue(body, out size);
            }
        }

        if (size <= 0)
        {
            size = MethodBodies.ComputeSize((byte*)body);
        }

        var bytes = NativeBuffers.ReadBytes(body, size);
        var state = GetModule(module);
        lock (state.Sync)
        {
            state.NewBodies[methodToken & 0x00FFFFFF] = bytes;
        }

        AotLog.Debug($"[{state.Module.Name}] SetILFunctionBody 0x{methodToken:x8} ({bytes.Length} bytes)");
    }

    // ---------------------------------------------------------------- IUnknown

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        if (guid == IUnknown.Guid || guid == ICorProfilerInfo.Guid || guid == ICorProfilerInfo2.Guid || guid == ICorProfilerInfo3.Guid ||
            guid == ICorProfilerInfo4.Guid || guid == ICorProfilerInfo5.Guid || guid == ICorProfilerInfo6.Guid || guid == ICorProfilerInfo7.Guid ||
            guid == ICorProfilerInfo8.Guid)
        {
            ptr = _info;
            return HResult.S_OK;
        }

        if (guid == ICorProfilerInfo12.Guid)
        {
            ptr = _info12;
            return HResult.S_OK;
        }

        AotLog.Debug($"ICorProfilerInfo.QueryInterface: declining {guid}");
        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef() => 1;

    public int Release() => 1;

    // ---------------------------------------------------------------- events and runtime

    public HResult GetEventMask(out int pdwEvents)
    {
        pdwEvents = (int)EventsLow;
        return HResult.S_OK;
    }

    public HResult SetEventMask(CorPrfMonitor dwEvents)
    {
        EventsLow = dwEvents;
        return HResult.S_OK;
    }

    public HResult GetEventMask2(out CorPrfMonitor pdwEventsLow, out CorPrfHighMonitor pdwEventsHigh)
    {
        pdwEventsLow = EventsLow;
        pdwEventsHigh = EventsHigh;
        return HResult.S_OK;
    }

    public HResult SetEventMask2(CorPrfMonitor dwEventsLow, CorPrfHighMonitor dwEventsHigh)
    {
        EventsLow = dwEventsLow;
        EventsHigh = dwEventsHigh;
        return HResult.S_OK;
    }

    public HResult GetRuntimeInformation(out ushort pClrInstanceId, out COR_PRF_RUNTIME_TYPE pRuntimeType, out ushort pMajorVersion, out ushort pMinorVersion, out ushort pBuildNumber, out ushort pQFEVersion, uint cchVersionString, out uint pcchVersionString, char* szVersionString)
    {
        pClrInstanceId = 0;
        pRuntimeType = COR_PRF_RUNTIME_TYPE.COR_PRF_CORE_CLR;
        pMajorVersion = (ushort)RuntimeVersion.Major;
        pMinorVersion = (ushort)RuntimeVersion.Minor;
        pBuildNumber = (ushort)Math.Max(0, RuntimeVersion.Build);
        pQFEVersion = 0;
        uint length;
        var hr = NativeBuffers.WriteString(RuntimeVersion.ToString(3), szVersionString, cchVersionString, &length);
        pcchVersionString = length;
        return hr;
    }

    public HResult InitializeCurrentThread() => HResult.S_OK;

    public HResult GetCurrentThreadId(out ThreadId pThreadId)
    {
        pThreadId = new ThreadId((nuint)Environment.CurrentManagedThreadId);
        return HResult.S_OK;
    }

    public HResult GetThreadAppDomain(ThreadId threadId, out AppDomainId pAppDomainId)
    {
        pAppDomainId = new AppDomainId(AppDomain);
        return HResult.S_OK;
    }

    public HResult GetAppDomainInfo(AppDomainId appDomainId, uint cchName, uint* pcchName, char* szName, ProcessId* pProcessId)
    {
        NativeBuffers.Set(pProcessId, new ProcessId(Environment.ProcessId));
        return NativeBuffers.WriteString("DefaultDomain", szName, cchName, pcchName);
    }

    public HResult ApplyMetaData(ModuleId moduleId) => HResult.S_OK;

    // ---------------------------------------------------------------- modules and assemblies

    public HResult GetModuleInfo(ModuleId moduleId, nint* ppBaseLoadAddress, uint cchName, uint* pcchName, char* szName, AssemblyId* pAssemblyId)
        => GetModuleInfo2(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId, null);

    public HResult GetModuleInfo2(ModuleId moduleId, nint* ppBaseLoadAddress, uint cchName, uint* pcchName, char* szName, AssemblyId* pAssemblyId, int* pdwModuleFlags)
    {
        ModuleState? state;
        lock (_sync)
        {
            if (!_modules.TryGetValue((int)moduleId.Value, out state))
            {
                return HResult.E_INVALIDARG;
            }
        }

        NativeBuffers.Set(ppBaseLoadAddress, (nint)0);
        NativeBuffers.Set(pAssemblyId, new AssemblyId(state.AssemblyId));
        // COR_PRF_MODULE_DISK | COR_PRF_MODULE_FLAT_LAYOUT
        NativeBuffers.Set(pdwModuleFlags, 0x1 | 0x20);
        return NativeBuffers.WriteString(state.Path, szName, cchName, pcchName);
    }

    public HResult GetAssemblyInfo(AssemblyId assemblyId, uint cchName, uint* pcchName, char* szName, out AppDomainId pAppDomainId, out ModuleId pModuleId)
    {
        pAppDomainId = new AppDomainId(AppDomain);
        pModuleId = new ModuleId(assemblyId.Value);
        ModuleState? state;
        lock (_sync)
        {
            if (!_modules.TryGetValue((int)assemblyId.Value, out state))
            {
                return HResult.E_INVALIDARG;
            }
        }

        return NativeBuffers.WriteString(state.Module.Assembly?.Name ?? state.Module.Name, szName, cchName, pcchName);
    }

    public HResult GetModuleMetaData(ModuleId moduleId, CorOpenFlags dwOpenFlags, in Guid riid, out IntPtr ppOut)
    {
        ModuleMetadata? metadata;
        lock (_sync)
        {
            if (!_metadata.TryGetValue((int)moduleId.Value, out metadata))
            {
                ppOut = IntPtr.Zero;
                return HResult.E_INVALIDARG;
            }
        }

        return metadata.QueryInterface(riid, out ppOut);
    }

    public HResult EnumModules(out void* ppEnum)
    {
        var ids = Modules.Select(m => (nint)m.Id).ToArray();
        var wrapper = NativeObjects.ICorProfilerModuleEnum.Wrap(new ModuleEnum(ids));
        NativeObjectRoots.Add(wrapper);
        ppEnum = (void*)(IntPtr)wrapper;
        return HResult.S_OK;
    }

    // ---------------------------------------------------------------- functions

    public HResult GetFunctionInfo(FunctionId functionId, ClassId* pClassId, ModuleId* pModuleId, MdToken* pToken)
    {
        (int Module, uint MethodToken) function;
        lock (_sync)
        {
            if (!_functions.TryGetValue(functionId.Value, out function))
            {
                return HResult.E_INVALIDARG;
            }
        }

        var state = GetModule(function.Module);
        NativeBuffers.Set(pClassId, new ClassId((nint)state.Metadata.GetOwnerTypeOfMethod(function.MethodToken & 0x00FFFFFF)));
        NativeBuffers.Set(pModuleId, new ModuleId(function.Module));
        NativeBuffers.Set(pToken, new MdToken((int)function.MethodToken));
        return HResult.S_OK;
    }

    public HResult GetFunctionFromToken(ModuleId moduleId, MdToken token, out FunctionId pFunctionId)
    {
        pFunctionId = new FunctionId(FunctionIdFor((int)moduleId.Value, (uint)token.Value));
        return HResult.S_OK;
    }

    public HResult GetTokenAndMetaDataFromFunction(FunctionId functionId, in Guid riid, out void* ppImport, out MdToken pToken)
    {
        (int Module, uint MethodToken) function;
        lock (_sync)
        {
            if (!_functions.TryGetValue(functionId.Value, out function))
            {
                ppImport = null;
                pToken = default;
                return HResult.E_INVALIDARG;
            }
        }

        pToken = new MdToken((int)function.MethodToken);
        var hr = _metadata[function.Module].QueryInterface(riid, out var import);
        ppImport = (void*)import;
        return hr;
    }

    public HResult GetClassFromToken(ModuleId moduleId, MdTypeDef typeDef, out ClassId pClassId)
    {
        pClassId = new ClassId((nint)(((long)moduleId.Value << 32) | (uint)typeDef.Value));
        return HResult.S_OK;
    }

    // ---------------------------------------------------------------- IL bodies and ReJIT

    public HResult GetILFunctionBody(ModuleId moduleId, MdMethodDef methodId, IntPtr* ppMethodHeader, uint* pcbMethodSize)
    {
        var key = ((int)moduleId.Value, (uint)methodId.Value);
        var state = GetModule(key.Item1);
        byte[]? bytes;
        lock (state.Sync)
        {
            state.NewBodies.TryGetValue((uint)methodId.Value & 0x00FFFFFF, out bytes);
        }

        bytes ??= MethodBodies.ReadOriginal(state, (uint)methodId.Value & 0x00FFFFFF);
        if (bytes == null)
        {
            return NativeBuffers.RecordNotFound;
        }

        IntPtr pointer;
        lock (_sync)
        {
            if (!_originalBodies.TryGetValue(key, out pointer))
            {
                pointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
                _originalBodies[key] = pointer;
            }
        }

        NativeBuffers.Set(ppMethodHeader, pointer);
        NativeBuffers.Set(pcbMethodSize, (uint)bytes.Length);
        return HResult.S_OK;
    }

    public HResult GetILFunctionBodyAllocator(ModuleId moduleId, IntPtr* ppMalloc)
    {
        NativeBuffers.Set(ppMalloc, (IntPtr)_malloc);
        return HResult.S_OK;
    }

    public HResult SetILFunctionBody(ModuleId moduleId, MdMethodDef methodid, IntPtr pbNewILMethodHeader)
    {
        StoreNewBody((int)moduleId.Value, (uint)methodid.Value, pbNewILMethodHeader, 0);
        return HResult.S_OK;
    }

    public HResult SetILInstrumentedCodeMap(FunctionId functionId, int fStartJit, uint cILMapEntries, CorIlMap* rgILMapEntries) => HResult.S_OK;

    public HResult RequestReJIT(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds)
    {
        for (var i = 0; i < cFunctions; i++)
        {
            PendingReJit.Enqueue(((int)moduleIds[i].Value, (uint)methodIds[i].Value));
        }

        Interlocked.Add(ref _reJitRequests, (int)cFunctions);
        AotLog.Debug($"RequestReJIT: {cFunctions} methods");
        return HResult.S_OK;
    }

    public HResult RequestRevert(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds, HResult* status)
    {
        for (var i = 0; i < cFunctions; i++)
        {
            if (status != null)
            {
                status[i] = HResult.S_OK;
            }
        }

        return HResult.S_OK;
    }
}
#endif
