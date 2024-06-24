using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Datadog.Trace.Tools.AotProcessor.Interfaces;
using Mono.Cecil;

namespace Datadog.Trace.Tools.AotProcessor.Runtime;

internal partial class Rewriter
{
    #region ICorProfilerInfo8 implementation

    public HResult ApplyMetaData(ModuleId moduleId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult BeginInprocDebugging(int fThisThreadOnly, out int pdwProfilerContext)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult DoStackSnapshot(ThreadId thread, delegate* unmanaged[Stdcall]<FunctionId, nint, COR_PRF_FRAME_INFO, uint, byte*, void*, HResult> callback, uint infoFlags, void* clientData, byte* context, uint contextSize)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult EndInprocDebugging(int dwProfilerContext)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult EnumJITedFunctions(out void* ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult EnumJITedFunctions2(out IntPtr ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult EnumModuleFrozenObjects(ModuleId moduleID, out void* ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult EnumModules(out void* ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult EnumNgenModuleMethodsInliningThisMethod(ModuleId inlinersModuleId, ModuleId inlineeModuleId, MdMethodDef inlineeMethodId, out int incompleteData, out IntPtr ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult EnumThreads(out IntPtr ppEnum)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult ForceGC()
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetAppDomainsContainingModule(ModuleId moduleId, uint cAppDomainIds, out uint pcAppDomainIds, AppDomainId* appDomainIds)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetAppDomainStaticAddress(ClassId classId, MdFieldDef fieldToken, AppDomainId appDomainId, out void* ppAddress)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetArrayObjectInfo(ObjectId objectId, uint cDimensions, out uint* pDimensionSizes, out int* pDimensionLowerBounds, out byte* ppData)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetBoxClassLayout(ClassId classId, out uint pBufferOffset)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetClassFromObject(ObjectId objectId, out ClassId pClassId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetClassFromToken(ModuleId moduleId, MdTypeDef typeDef, out ClassId pClassId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassFromTokenAndTypeArgs(ModuleId moduleID, MdTypeDef typeDef, uint cTypeArgs, ClassId* typeArgs, out ClassId pClassID)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetClassIdInfo(ClassId ClassId, out ModuleId pModuleId, out MdTypeDef pTypeDefToken)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassIDInfo2(ClassId classId, out ModuleId pModuleId, out MdTypeDef pTypeDefToken, out ClassId pParentClassId, uint cNumTypeArgs, out uint pcNumTypeArgs, out ClassId* typeArgs)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetClassLayout(ClassId classID, out COR_FIELD_OFFSET* rFieldOffset, uint cFieldOffset, out uint pcFieldOffset, out uint pulClassSize)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo(FunctionId FunctionId, out byte* pStart, out uint pcSize)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo2(FunctionId functionID, uint cCodeInfos, out uint pcCodeInfos, out COR_PRF_CODE_INFO* codeInfos)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetCodeInfo3(FunctionId functionID, ReJITId reJitId, uint cCodeInfos, out uint pcCodeInfos, COR_PRF_CODE_INFO* codeInfos)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetContextStaticAddress(ClassId classId, MdFieldDef fieldToken, ContextId contextId, out void* ppAddress)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetCurrentThreadId(out ThreadId pThreadId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetEventMask(out int pdwEvents)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionEnter3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo, int* pcbArgumentInfo, COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetFunctionFromIP(nint ip, out FunctionId pFunctionId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromIP2(byte* ip, out FunctionId pFunctionId, out ReJITId pReJitId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetFunctionFromToken(ModuleId moduleId, MdToken token, out FunctionId pFunctionId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromTokenAndTypeArgs(ModuleId moduleID, MdMethodDef funcDef, ClassId classId, uint cTypeArgs, ClassId* typeArgs, out FunctionId pFunctionID)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionInfo2(FunctionId funcId, COR_PRF_FRAME_INFO frameInfo, out ClassId pClassId, out ModuleId pModuleId, out MdToken pToken, uint cTypeArgs, out uint pcTypeArgs, out ClassId* typeArgs)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetFunctionLeave3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo, out COR_PRF_FUNCTION_ARGUMENT_RANGE pRetvalRange)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetFunctionTailcall3Info(FunctionId functionId, COR_PRF_ELT_INFO eltInfo, out COR_PRF_FRAME_INFO pFrameInfo)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetGenerationBounds(uint cObjectRanges, out uint pcObjectRanges, out COR_PRF_GC_GENERATION_RANGE* ranges)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetHandleFromThread(ThreadId ThreadId, out nint phThread)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetILToNativeMapping(FunctionId FunctionId, uint cMap, out uint pcMap, CorDebugIlToNativeMap* map)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetILToNativeMapping2(FunctionId functionId, ReJITId reJitId, uint cMap, uint* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP* map)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetInMemorySymbolsLength(ModuleId moduleId, out int pCountSymbolBytes)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetInprocInspectionInterface(out void* ppicd)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetInprocInspectionIThisThread(out void* ppicd)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetNotifiedExceptionClauseInfo(out COR_PRF_EX_CLAUSE_INFO pinfo)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetObjectGeneration(ObjectId objectId, out COR_PRF_GC_GENERATION_RANGE range)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetObjectSize(ObjectId objectId, out uint pcSize)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetObjectSize2(ObjectId objectId, out nint pcSize)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetReJITIDs(FunctionId functionId, uint cReJitIds, uint* pcReJitIds, ReJITId* reJitIds)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetRVAStaticAddress(ClassId classId, MdFieldDef fieldToken, out void* ppAddress)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetStaticFieldInfo(ClassId classId, MdFieldDef fieldToken, out COR_PRF_STATIC_TYPE pFieldInfo)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetStringLayout(out uint pBufferLengthOffset, out uint pStringLengthOffset, out uint pBufferOffset)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetStringLayout2(out uint pStringLengthOffset, out uint pBufferOffset)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetThreadAppDomain(ThreadId threadId, out AppDomainId pAppDomainId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetThreadContext(ThreadId ThreadId, out ContextId pContextId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult GetThreadInfo(ThreadId ThreadId, out int pdwWin32ThreadId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetThreadStaticAddress(ClassId classId, MdFieldDef fieldToken, ThreadId threadId, out void* ppAddress)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetThreadStaticAddress2(ClassId classId, MdFieldDef fieldToken, AppDomainId appDomainId, ThreadId threadId, out void* ppAddress)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetTokenAndMetaDataFromFunction(FunctionId FunctionId, out Guid riid, out void* ppImport, out MdToken pToken)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult IsArrayClass(ClassId ClassId, out CorElementType pBaseElemType, out ClassId pBaseClassId, out uint pcRank)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult ReadInMemorySymbols(ModuleId moduleId, int symbolsReadOffset, byte* pSymbolBytes, int countSymbolBytes, out int pCountSymbolBytesRead)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult RequestProfilerDetach(int dwExpectedCompletionMilliseconds)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
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

    public unsafe HResult SetEnterLeaveFunctionHooks(void* pFuncEnter, void* pFuncLeave, void* pFuncTailcall)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks2(void* pFuncEnter, void* pFuncLeave, void* pFuncTailcall)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks3(void* pFuncEnter3, void* pFuncLeave3, void* pFuncTailcall3)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetEnterLeaveFunctionHooks3WithInfo(void* pFuncEnter3WithInfo, void* pFuncLeave3WithInfo, void* pFuncTailcall3WithInfo)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult SetEventMask(CorPrfMonitor dwEvents)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetFunctionIdMapper(void* pFunc)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetFunctionIDMapper2(delegate* unmanaged[Stdcall]<FunctionId, void*, int*, nint> pFunc, void* clientData)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult SetFunctionReJIT(FunctionId functionId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult SetILInstrumentedCodeMap(FunctionId FunctionId, int fStartJit, uint cILMapEntries, CorIlMap* rgILMapEntries)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public HResult IsFunctionDynamic(FunctionId functionId, out int isDynamic)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetFunctionFromIP3(nint ip, FunctionId* functionId, out ReJITId pReJitId)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    public unsafe HResult GetDynamicFunctionInfo(FunctionId functionId, out ModuleId moduleId, byte* ppvSig, out uint pbSig, uint cchName, out uint pcchName, char* wszName)
    {
        System.Diagnostics.Debugger.Break();
        throw new NotImplementedException();
    }

    #endregion
}
