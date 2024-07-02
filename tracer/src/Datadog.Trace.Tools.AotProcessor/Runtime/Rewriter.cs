using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime;

[Guid("0D53A3E8-E51A-49C7-944E-E72A2064F938")]
internal partial class Rewriter : ICorProfilerInfo8, IMethodMalloc, IDisposable
{
    private NativeObjects.ICorProfilerInfo8 corProfilerInfo;
    private NativeObjects.IMethodMalloc methodMalloc;
    private NativeObjects.ICorProfilerCallback4Invoker profiler;
    private bool initialized = false;

    private CorPrfMonitor eventsLow = CorPrfMonitor.COR_PRF_MONITOR_NONE;
    private CorPrfHighMonitor eventsHigh = CorPrfHighMonitor.COR_PRF_HIGH_MONITOR_NONE;

    private AppDomainInfo appDomainInfo = new AppDomainInfo(1, "AoT");
    private Dictionary<nint, AssemblyInfo> assemblies = new Dictionary<nint, AssemblyInfo>();
    private Dictionary<nint, ModuleInfo> modules = new Dictionary<nint, ModuleInfo>();
    private Dictionary<nint, MethodInfo> functions = new Dictionary<nint, MethodInfo>();

    private Dictionary<IntPtr, uint> buffers = new Dictionary<IntPtr, uint>();

    private string outputPath = string.Empty;

    public Rewriter()
    {
        corProfilerInfo = NativeObjects.ICorProfilerInfo8.Wrap(this);
        methodMalloc = NativeObjects.IMethodMalloc.Wrap(this);
    }

    public void Dispose()
    {
        Shutdown();
        corProfilerInfo.Dispose();
        methodMalloc.Dispose();
    }

    public bool Init()
    {
        if (initialized) { return true; }

        var callback = ProfilerInterop.LoadProfiler(this);
        if (callback == null)
        {
            return false;
        }

        profiler = callback.Value;

        // Init Instrumentation -> We must tell the instrumenter is AOT, so we can provide what profiler provides by auto instrumentation in runtime
        ClrProfiler.Instrumentation.Initialize(true);

        initialized = true;
        return true;
    }

    public void Shutdown()
    {
        if (!initialized) { return; }

        profiler.Shutdown();
        initialized = false;
    }

    public void SetOutput(string output)
    {
        // Set output path
        outputPath = Path.GetFullPath(output);
        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }
    }

    public unsafe void ProcessApp(string appAssemblyPath, string otputPath = "./output")
    {
        var references = new HashSet<string>();
        var assemblies = new Queue<string>();

        SetOutput(otputPath);
        var folder = Path.GetDirectoryName(appAssemblyPath) ?? string.Empty;
        var readParams = new ReaderParameters(ReadingMode.Immediate);

        AddAssembly(Path.Combine(folder, "System.Runtime.dll"));
        AddAssembly(Path.GetFullPath("Datadog.Trace.dll"));
        AddAssembly(appAssemblyPath);

        while (assemblies.Count > 0)
        {
            var path = assemblies.Dequeue();
            if (!File.Exists(path))
            {
                continue;
            }

            AssemblyDefinition assembly;
            try
            {
                assembly = AssemblyDefinition.ReadAssembly(path, readParams);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to read assembly: {0}", ex.Message);
                continue;
            }

            ProcessAssembly(assembly);

            foreach (var reference in assembly.MainModule.AssemblyReferences)
            {
                AddAssembly(Path.Combine(folder, reference.Name + ".dll"));
            }
        }

        void AddAssembly(string path)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            if (!references!.Contains(fileName))
            {
                references.Add(fileName);
                assemblies!.Enqueue(path);
            }
        }
    }

    internal void InitAppDomain()
    {
        // AppDomain
        if (eventsLow.HasFlag(CorPrfMonitor.COR_PRF_MONITOR_APPDOMAIN_LOADS))
        {
            profiler.AppDomainCreationStarted(appDomainInfo.Id);
            profiler.AppDomainCreationFinished(appDomainInfo.Id, HResult.S_OK);
        }
    }

    internal unsafe bool ProcessAssembly(AssemblyDefinition assembly)
    {
        var path = assembly.MainModule.FileName;
        Console.Write("Processing assembly: {0} ...", Path.GetFileName(path));

        if ((assembly.MainModule.Attributes & ModuleAttributes.ILOnly) == 0)
        {
            Console.Write(" Mixed assembly. ");
            assembly.MainModule.Attributes |= ModuleAttributes.ILOnly;
        }

        // Assembly
        var assemblyInfo = new AssemblyInfo(this, assembly, assemblies.Count + 1, appDomainInfo, assembly.Name.Name, path, GetModuleId);
        assemblies[assemblyInfo.Id.Value] = assemblyInfo;
        if (eventsLow.HasFlag(CorPrfMonitor.COR_PRF_MONITOR_ASSEMBLY_LOADS))
        {
            profiler.AssemblyLoadStarted(assemblyInfo.Id);
        }

        // Module
        var moduleInfo = assemblyInfo.MainModule;
        modules[moduleInfo.Id.Value] = moduleInfo;
        if (eventsLow.HasFlag(CorPrfMonitor.COR_PRF_MONITOR_MODULE_LOADS))
        {
            profiler.ModuleLoadStarted(moduleInfo.Id);
            profiler.ModuleLoadFinished(moduleInfo.Id, HResult.S_OK);
        }

        if (eventsLow.HasFlag(CorPrfMonitor.COR_PRF_MONITOR_ASSEMBLY_LOADS))
        {
            profiler.AssemblyLoadFinished(assemblyInfo.Id, HResult.S_OK);
        }

        // Process all methods in the assembly
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (eventsLow.HasFlag(CorPrfMonitor.COR_PRF_MONITOR_JIT_COMPILATION))
                {
                    var methodInfo = moduleInfo.GetMember(method) as MethodInfo;
                    if (methodInfo is null) { continue; }

                    var functionId = new FunctionId(functions.Count + 1);
                    functions[functionId.Value] = methodInfo;

                    // JIT Instrument method
                    profiler.JITCompilationStarted(functionId, 1);
                }
            }
        }

        // Write processed assembly
        if (assembly.MainModule.IsDirty)
        {
            var output = Path.Combine(outputPath, Path.GetFileName(path));
            var writeParams = new WriterParameters();
            writeParams.Raw = true;
            assembly.Write(output, writeParams);

            Console.WriteLine(" Written.");
        }
        else
        {
            Console.WriteLine(" Unchanged.");
        }

        return true;
    }

    public AssemblyInfo? GetAssemblyInfo(int id)
    {
        assemblies.TryGetValue(id, out var res);
        return res;
    }

    private int GetModuleId()
    {
        return modules.Count + 1;
    }

    #region IMethodMalloc

    public IntPtr Alloc(uint cb)
    {
        var res = Marshal.AllocCoTaskMem((int)cb);
        buffers[res] = cb;
        return res;
    }

    #endregion

    #region ICorProfilerInfo8 implementation

    public HResult QueryInterface(in Guid guid, out IntPtr ptr)
    {
        if (guid == IUnknown.Guid ||
            guid == ICorProfilerInfo.Guid ||
                guid == ICorProfilerInfo2.Guid ||
                guid == ICorProfilerInfo3.Guid ||
                guid == ICorProfilerInfo4.Guid ||
                guid == ICorProfilerInfo5.Guid ||
                guid == ICorProfilerInfo6.Guid ||
                guid == ICorProfilerInfo7.Guid ||
                guid == ICorProfilerInfo8.Guid)
        {
            ptr = corProfilerInfo;
            return HResult.S_OK;
        }

        ptr = IntPtr.Zero;
        return HResult.E_NOINTERFACE;
    }

    public int AddRef()
    {
        return 1;
    }

    public int Release()
    {
        return 1;
    }

    public HResult GetEventMask2(out CorPrfMonitor pdwEventsLow, out CorPrfHighMonitor pdwEventsHigh)
    {
        pdwEventsLow = this.eventsLow;
        pdwEventsHigh = this.eventsHigh;
        return HResult.S_OK;
    }

    public HResult SetEventMask2(CorPrfMonitor dwEventsLow, CorPrfHighMonitor dwEventsHigh)
    {
        this.eventsLow = dwEventsLow;
        this.eventsHigh = dwEventsHigh;
        return HResult.S_OK;
    }

    public HResult InitializeCurrentThread()
    {
        return HResult.S_OK;
    }

    public unsafe HResult GetRuntimeInformation(out ushort pClrInstanceId, out COR_PRF_RUNTIME_TYPE pRuntimeType, out ushort pMajorVersion, out ushort pMinorVersion, out ushort pBuildNumber, out ushort pQFEVersion, uint cchVersionString, out uint pcchVersionString, char* szVersionString)
    {
        pRuntimeType = COR_PRF_RUNTIME_TYPE.COR_PRF_CORE_CLR;
        pQFEVersion = default;
        pcchVersionString = default;
        pClrInstanceId = default;
        pMajorVersion = 6;
        pMinorVersion = 0;
        pBuildNumber = default;
        return HResult.S_OK;
    }

    public unsafe HResult GetAssemblyInfo(AssemblyId assemblyId, uint cchName, uint* pcchName, char* szName, out AppDomainId pAppDomainId, out ModuleId pModuleId)
    {
        var assemblyInfo = assemblies[assemblyId.Value];
        assemblyInfo.Name.CopyTo(cchName, szName, pcchName);
        pAppDomainId = assemblyInfo.AppDomain.Id;
        pModuleId = assemblyInfo.MainModule.Id;
        return HResult.S_OK;
    }

    public unsafe HResult GetAppDomainInfo(AppDomainId appDomainId, uint cchName, uint* pcchName, char* szName, ProcessId* pProcessId)
    {
        appDomainInfo.Name.CopyTo(cchName, szName, pcchName);
        if (pProcessId is not null)
        {
            *pProcessId = appDomainInfo.ProcessId;
        }

        return HResult.S_OK;
    }

    public unsafe HResult GetModuleInfo(ModuleId moduleId, nint* ppBaseLoadAddress, uint cchName, uint* pcchName, char* szName, AssemblyId* pAssemblyId)
    {
        return GetModuleInfo2(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId, null);
    }

    public unsafe HResult GetModuleInfo2(ModuleId moduleId, nint* ppBaseLoadAddress, uint cchName, uint* pcchName, char* szName, AssemblyId* pAssemblyId, int* pdwModuleFlags)
    {
        var moduleInfo = modules[moduleId.Value];
        if (ppBaseLoadAddress is not null)
        {
            *ppBaseLoadAddress = 0;
        }

        moduleInfo.Path.CopyTo(cchName, szName, pcchName);
        if (pAssemblyId is not null)
        {
            *pAssemblyId = moduleInfo.Assembly.Id;
        }

        if (pdwModuleFlags is not null)
        {
            *pdwModuleFlags = (int)moduleInfo.Flags;
        }

        return HResult.S_OK;
    }

    public HResult GetModuleMetaData(ModuleId moduleId, CorOpenFlags dwOpenFlags, Guid riid, out IntPtr ppOut)
    {
        var moduleInfo = modules[moduleId.Value];
        return moduleInfo.MetadataImport.QueryInterface(riid, out ppOut);
    }

    public HResult GetFunctionInfo(FunctionId functionId, out ClassId pClassId, out ModuleId pModuleId, out MdToken pToken)
    {
        if (functions.TryGetValue(functionId.Value, out var methodInfo))
        {
            pClassId = new ClassId(methodInfo.Definition.DeclaringType.MetadataToken.ToInt32());
            pModuleId = methodInfo.Module.Id;
            pToken = new MdToken(methodInfo.Id.Value);

            return HResult.S_OK;
        }

        pClassId = default;
        pModuleId = default;
        pToken = default;
        return HResult.E_INVALIDARG;
    }

    public unsafe HResult GetILFunctionBodyAllocator(ModuleId moduleId, IntPtr* ppMalloc)
    {
        *ppMalloc = methodMalloc;
        return HResult.S_OK;
    }

    public HResult SetILFunctionBody(ModuleId moduleId, MdMethodDef methodId, IntPtr pbNewILMethodHeader)
    {
        var moduleInfo = modules[moduleId.Value];
        var method = moduleInfo.Definition.LookupToken(methodId.Value) as MethodDefinition;
        if (method is null) { return HResult.E_INVALIDARG; }

        var bufferLen = buffers[pbNewILMethodHeader];
        byte[] rawBody = new byte[bufferLen];
        Marshal.Copy(pbNewILMethodHeader, rawBody, 0, (int)bufferLen);

        Marshal.FreeCoTaskMem(pbNewILMethodHeader);
        buffers.Remove(pbNewILMethodHeader);

        method.Body.RawBody = rawBody;

        return HResult.S_OK;
    }

    public unsafe HResult GetILFunctionBody(ModuleId moduleId, MdMethodDef methodId, IntPtr* ppMethodHeader, uint* pcbMethodSize)
    {
        var moduleInfo = modules[moduleId.Value];
        var method = moduleInfo.Definition.LookupToken(methodId.Value) as MethodDefinition;
        if (method is null) { return HResult.E_INVALIDARG; }

        var methodInfo = moduleInfo.GetMember(method) as MethodInfo;
        if (methodInfo is null) { return HResult.E_INVALIDARG; }

        if (ppMethodHeader is not null)
        {
            *ppMethodHeader = methodInfo.GetRawBody();
        }

        if (pcbMethodSize is not null)
        {
            *pcbMethodSize = (uint)method.Body.RawBody.Length;
        }

        return HResult.S_OK;
    }

    public unsafe HResult RequestReJIT(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult RequestRevert(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds, HResult* status)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    #endregion
}
