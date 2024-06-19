using System;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.AotProcessor.Interfaces;

namespace Datadog.Trace.Tools;

[Guid("0D53A3E8-E51A-49C7-944E-E72A2064F938")]
internal class Rewriter : ICorProfilerInfo8, IDisposable
{
    private NativeObjects.ICorProfilerInfo8 _corProfilerInfo;

    private CorPrfMonitor eventsLow = CorPrfMonitor.COR_PRF_MONITOR_NONE;
    private CorPrfHighMonitor eventsHigh = CorPrfHighMonitor.COR_PRF_HIGH_MONITOR_NONE;

    public Rewriter()
    {
        _corProfilerInfo = NativeObjects.ICorProfilerInfo8.Wrap(this);
    }

    public void Dispose()
    {
        _corProfilerInfo.Dispose();
    }

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
            ptr = _corProfilerInfo;
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

    public HResult ApplyMetaData(ModuleId moduleId)
    {
        throw new NotImplementedException();
    }

    public HResult BeginInprocDebugging(int fThisThreadOnly, out int pdwProfilerContext)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult DoStackSnapshot(ThreadId thread, delegate* unmanaged[Stdcall]<FunctionId, nint, COR_PRF_FRAME_INFO, uint, byte*, void*, HResult> callback, uint infoFlags, void* clientData, byte* context, uint contextSize)
    {
        throw new NotImplementedException();
    }

    public HResult EndInprocDebugging(int dwProfilerContext)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult EnumJITedFunctions(out void* ppEnum)
    {
        throw new NotImplementedException();
    }

    public HResult EnumJITedFunctions2(out IntPtr ppEnum)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult EnumModuleFrozenObjects(ModuleId moduleID, out void* ppEnum)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult EnumModules(out void* ppEnum)
    {
        throw new NotImplementedException();
    }

    public HResult EnumNgenModuleMethodsInliningThisMethod(ModuleId inlinersModuleId, ModuleId inlineeModuleId, MdMethodDef inlineeMethodId, out int incompleteData, out IntPtr ppEnum)
    {
        throw new NotImplementedException();
    }

    public HResult EnumThreads(out IntPtr ppEnum)
    {
        throw new NotImplementedException();
    }

    public HResult ForceGC()
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetAppDomainInfo(AppDomainId appDomainId, uint cchName, out uint pcchName, char* szName, out ProcessId pProcessId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetAppDomainsContainingModule(ModuleId moduleId, uint cAppDomainIds, out uint pcAppDomainIds, AppDomainId* appDomainIds)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetAppDomainStaticAddress(ClassId classId, MdFieldDef fieldToken, AppDomainId appDomainId, out void* ppAddress)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetArrayObjectInfo(ObjectId objectId, uint cDimensions, out uint* pDimensionSizes, out int* pDimensionLowerBounds, out byte* ppData)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetAssemblyInfo(AssemblyId assemblyId, uint cchName, out uint pcchName, char* szName, out AppDomainId pAppDomainId, out ModuleId pModuleId)
    {
        throw new NotImplementedException();
    }

    public HResult GetBoxClassLayout(ClassId classId, out uint pBufferOffset)
    {
        throw new NotImplementedException();
    }

    public HResult GetClassFromObject(ObjectId ObjectId, out ClassId pClassId)
    {
        throw new NotImplementedException();
    }

    public HResult GetClassFromToken(ModuleId ModuleId, MdTypeDef typeDef, out ClassId pClassId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassFromTokenAndTypeArgs(ModuleId moduleID, MdTypeDef typeDef, uint cTypeArgs, ClassId* typeArgs, out ClassId pClassID)
    {
        throw new NotImplementedException();
    }

    public HResult GetClassIdInfo(ClassId ClassId, out ModuleId pModuleId, out MdTypeDef pTypeDefToken)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassIDInfo2(ClassId classId, out ModuleId pModuleId, out MdTypeDef pTypeDefToken, out ClassId pParentClassId, uint cNumTypeArgs, out uint pcNumTypeArgs, out ClassId* typeArgs)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassLayout(ClassId classID, out COR_FIELD_OFFSET* rFieldOffset, uint cFieldOffset, out uint pcFieldOffset, out uint pulClassSize)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo(FunctionId FunctionId, out byte* pStart, out uint pcSize)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo2(FunctionId functionID, uint cCodeInfos, out uint pcCodeInfos, out COR_PRF_CODE_INFO* codeInfos)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo3(FunctionId functionID, ReJITId reJitId, uint cCodeInfos, out uint pcCodeInfos, COR_PRF_CODE_INFO* codeInfos)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetContextStaticAddress(ClassId classId, MdFieldDef fieldToken, ContextId contextId, out void* ppAddress)
    {
        throw new NotImplementedException();
    }

    public HResult GetCurrentThreadId(out ThreadId pThreadId)
    {
        throw new NotImplementedException();
    }

    public HResult GetEventMask(out int pdwEvents)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionEnter3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo, int* pcbArgumentInfo, COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo)
    {
        throw new NotImplementedException();
    }

    public HResult GetFunctionFromIP(nint ip, out FunctionId pFunctionId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromIP2(byte* ip, out FunctionId pFunctionId, out ReJITId pReJitId)
    {
        throw new NotImplementedException();
    }

    public HResult GetFunctionFromToken(ModuleId ModuleId, MdToken token, out FunctionId pFunctionId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromTokenAndTypeArgs(ModuleId moduleID, MdMethodDef funcDef, ClassId classId, uint cTypeArgs, ClassId* typeArgs, out FunctionId pFunctionID)
    {
        throw new NotImplementedException();
    }

    public HResult GetFunctionInfo(FunctionId FunctionId, out ClassId pClassId, out ModuleId pModuleId, out MdToken pToken)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionInfo2(FunctionId funcId, COR_PRF_FRAME_INFO frameInfo, out ClassId pClassId, out ModuleId pModuleId, out MdToken pToken, uint cTypeArgs, out uint pcTypeArgs, out ClassId* typeArgs)
    {
        throw new NotImplementedException();
    }

    public HResult GetFunctionLeave3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo, out COR_PRF_FUNCTION_ARGUMENT_RANGE pRetvalRange)
    {
        throw new NotImplementedException();
    }

    public HResult GetFunctionTailcall3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetGenerationBounds(uint cObjectRanges, out uint pcObjectRanges, out COR_PRF_GC_GENERATION_RANGE* ranges)
    {
        throw new NotImplementedException();
    }

    public HResult GetHandleFromThread(ThreadId ThreadId, out nint phThread)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetILFunctionBody(ModuleId ModuleId, MdMethodDef methodId, out byte* ppMethodHeader, out uint pcbMethodSize)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetILFunctionBodyAllocator(ModuleId ModuleId, out void* ppMalloc)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetILToNativeMapping(FunctionId FunctionId, uint cMap, out uint pcMap, CorDebugIlToNativeMap* map)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetILToNativeMapping2(FunctionId functionId, ReJITId reJitId, uint cMap, uint* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP* map)
    {
        throw new NotImplementedException();
    }

    public HResult GetInMemorySymbolsLength(ModuleId moduleId, out int pCountSymbolBytes)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetInprocInspectionInterface(out void* ppicd)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetInprocInspectionIThisThread(out void* ppicd)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetModuleInfo(ModuleId ModuleId, out nint ppBaseLoadAddress, uint cchName, out uint pcchName, char* szName, out AssemblyId pAssemblyId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetModuleInfo2(ModuleId moduleId, out byte* ppBaseLoadAddress, uint cchName, out uint pcchName, char* szName, out AssemblyId pAssemblyId, out int pdwModuleFlags)
    {
        throw new NotImplementedException();
    }

    public HResult GetModuleMetaData(ModuleId ModuleId, CorOpenFlags dwOpenFlags, Guid riid, out IntPtr ppOut)
    {
        throw new NotImplementedException();
    }

    public HResult GetNotifiedExceptionClauseInfo(out COR_PRF_EX_CLAUSE_INFO pinfo)
    {
        throw new NotImplementedException();
    }

    public HResult GetObjectGeneration(ObjectId objectId, out COR_PRF_GC_GENERATION_RANGE range)
    {
        throw new NotImplementedException();
    }

    public HResult GetObjectSize(ObjectId ObjectId, out uint pcSize)
    {
        throw new NotImplementedException();
    }

    public HResult GetObjectSize2(ObjectId objectId, out nint pcSize)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetReJITIDs(FunctionId functionId, uint cReJitIds, uint* pcReJitIds, ReJITId* reJitIds)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetRVAStaticAddress(ClassId classId, MdFieldDef fieldToken, out void* ppAddress)
    {
        throw new NotImplementedException();
    }

    public HResult GetStaticFieldInfo(ClassId classId, MdFieldDef fieldToken, out COR_PRF_STATIC_TYPE pFieldInfo)
    {
        throw new NotImplementedException();
    }

    public HResult GetStringLayout(out uint pBufferLengthOffset, out uint pStringLengthOffset, out uint pBufferOffset)
    {
        throw new NotImplementedException();
    }

    public HResult GetStringLayout2(out uint pStringLengthOffset, out uint pBufferOffset)
    {
        throw new NotImplementedException();
    }

    public HResult GetThreadAppDomain(ThreadId threadId, out AppDomainId pAppDomainId)
    {
        throw new NotImplementedException();
    }

    public HResult GetThreadContext(ThreadId ThreadId, out ContextId pContextId)
    {
        throw new NotImplementedException();
    }

    public HResult GetThreadInfo(ThreadId ThreadId, out int pdwWin32ThreadId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetThreadStaticAddress(ClassId classId, MdFieldDef fieldToken, ThreadId threadId, out void* ppAddress)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetThreadStaticAddress2(ClassId classId, MdFieldDef fieldToken, AppDomainId appDomainId, ThreadId threadId, out void* ppAddress)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetTokenAndMetaDataFromFunction(FunctionId FunctionId, out Guid riid, out void* ppImport, out MdToken pToken)
    {
        throw new NotImplementedException();
    }

    public HResult IsArrayClass(ClassId ClassId, out CorElementType pBaseElemType, out ClassId pBaseClassId, out uint pcRank)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult ReadInMemorySymbols(ModuleId moduleId, int symbolsReadOffset, byte* pSymbolBytes, int countSymbolBytes, out int pCountSymbolBytesRead)
    {
        throw new NotImplementedException();
    }

    public HResult RequestProfilerDetach(int dwExpectedCompletionMilliseconds)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult RequestReJIT(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult RequestRevert(uint cFunctions, ModuleId* moduleIds, MdMethodDef* methodIds, HResult* status)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks(void* pFuncEnter, void* pFuncLeave, void* pFuncTailcall)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks2(void* pFuncEnter, void* pFuncLeave, void* pFuncTailcall)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks3(void* pFuncEnter3, void* pFuncLeave3, void* pFuncTailcall3)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks3WithInfo(void* pFuncEnter3WithInfo, void* pFuncLeave3WithInfo, void* pFuncTailcall3WithInfo)
    {
        throw new NotImplementedException();
    }

    public HResult SetEventMask(CorPrfMonitor dwEvents)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetFunctionIdMapper(void* pFunc)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetFunctionIDMapper2(delegate* unmanaged[Stdcall]<FunctionId, void*, int*, nint> pFunc, void* clientData)
    {
        throw new NotImplementedException();
    }

    public HResult SetFunctionReJIT(FunctionId functionId)
    {
        throw new NotImplementedException();
    }

    public HResult SetILFunctionBody(ModuleId ModuleId, MdMethodDef methodid, byte pbNewILMethodHeader)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult SetILInstrumentedCodeMap(FunctionId FunctionId, int fStartJit, uint cILMapEntries, CorIlMap* rgILMapEntries)
    {
        throw new NotImplementedException();
    }

    public HResult IsFunctionDynamic(FunctionId functionId, out int isDynamic)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromIP3(nint ip, FunctionId* functionId, out ReJITId pReJitId)
    {
        throw new NotImplementedException();
    }

    public unsafe HResult GetDynamicFunctionInfo(FunctionId functionId, out ModuleId moduleId, byte* ppvSig, out uint pbSig, uint cchName, out uint pcchName, char* wszName)
    {
        throw new NotImplementedException();
    }
}
