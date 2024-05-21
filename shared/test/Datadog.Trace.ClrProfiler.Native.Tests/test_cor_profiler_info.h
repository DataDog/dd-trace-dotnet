#pragma once
#include <corhlpr.h>
#include <corprof.h>

class TestCorProfilerInfo : public ICorProfilerInfo4 
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(const IID& riid, void** ppvObject) override
    {
        return E_FAIL;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return 0;
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        return 0;
    }

    HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID* pClassId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetEventMask(DWORD* pdwEvents) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID* pFunctionId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID threadId, HANDLE* phThread) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID objectId, ULONG* pcSize) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId,
    ULONG* pcRank) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID* pThreadId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId, mdToken* pToken) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetEventMask(DWORD dwEvents) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave,
        FunctionTailcall* pFuncTailcall) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(FunctionIDMapper* pFunc) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID functionId, const IID& riid, IUnknown** ppImport,
        mdToken* pToken) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName,
        WCHAR szName[], AssemblyID* pAssemblyId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, const IID& riid, IUnknown** ppOut) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader,
        ULONG* pcbMethodSize) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc** ppMalloc) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
        ProcessID* pProcessId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
        AppDomainID* pAppDomainId, ModuleID* pModuleId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID functionId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE ForceGC() override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries,
        COR_IL_MAP rgILMapEntries[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown** ppicd) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown** ppicd) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID threadId, ContextID* pContextId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL fThisThreadOnly, DWORD* pdwProfilerContext) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD dwProfilerContext) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap,
        COR_DEBUG_IL_TO_NATIVE_MAP map[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE DoStackSnapshot(ThreadID thread, StackSnapshotCallback* callback, ULONG32 infoFlags,
        void* clientData, BYTE context[], ULONG32 contextSize) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks2(FunctionEnter2* pFuncEnter, FunctionLeave2* pFuncLeave,
        FunctionTailcall2* pFuncTailcall) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionInfo2(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo, ClassID* pClassId,
        ModuleID* pModuleId, mdToken* pToken, ULONG32 cTypeArgs, ULONG32* pcTypeArgs, ClassID typeArgs[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetStringLayout(ULONG* pBufferLengthOffset, ULONG* pStringLengthOffset, ULONG* pBufferOffset) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetClassLayout(ClassID classID, COR_FIELD_OFFSET rFieldOffset[], ULONG cFieldOffset,
        ULONG* pcFieldOffset, ULONG* pulClassSize) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetClassIDInfo2(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken,
        ClassID* pParentClassId, ULONG32 cNumTypeArgs, ULONG32* pcNumTypeArgs, ClassID typeArgs[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetCodeInfo2(FunctionID functionID, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
        COR_PRF_CODE_INFO codeInfos[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetClassFromTokenAndTypeArgs(ModuleID moduleID, mdTypeDef typeDef, ULONG32 cTypeArgs,
        ClassID typeArgs[], ClassID* pClassID) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionFromTokenAndTypeArgs(ModuleID moduleID, mdMethodDef funcDef, ClassID classId,
        ULONG32 cTypeArgs, ClassID typeArgs[], FunctionID* pFunctionID) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EnumModuleFrozenObjects(ModuleID moduleID, ICorProfilerObjectEnum** ppEnum) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetArrayObjectInfo(ObjectID objectId, ULONG32 cDimensions, ULONG32 pDimensionSizes[],
        int pDimensionLowerBounds[], BYTE** ppData) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetBoxClassLayout(ClassID classId, ULONG32* pBufferOffset) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetThreadAppDomain(ThreadID threadId, AppDomainID* pAppDomainId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetRVAStaticAddress(ClassID classId, mdFieldDef fieldToken, void** ppAddress) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetAppDomainStaticAddress(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
        void** ppAddress) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetThreadStaticAddress(ClassID classId, mdFieldDef fieldToken, ThreadID threadId,
        void** ppAddress) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetContextStaticAddress(ClassID classId, mdFieldDef fieldToken, ContextID contextId,
        void** ppAddress) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetStaticFieldInfo(ClassID classId, mdFieldDef fieldToken, COR_PRF_STATIC_TYPE* pFieldInfo) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetGenerationBounds(ULONG cObjectRanges, ULONG* pcObjectRanges,
        COR_PRF_GC_GENERATION_RANGE ranges[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetObjectGeneration(ObjectID objectId, COR_PRF_GC_GENERATION_RANGE* range) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetNotifiedExceptionClauseInfo(COR_PRF_EX_CLAUSE_INFO* pinfo) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EnumJITedFunctions(ICorProfilerFunctionEnum** ppEnum) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE RequestProfilerDetach(DWORD dwExpectedCompletionMilliseconds) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetFunctionIDMapper2(FunctionIDMapper2* pFunc, void* clientData) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetStringLayout2(ULONG* pStringLengthOffset, ULONG* pBufferOffset) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3(FunctionEnter3* pFuncEnter3, FunctionLeave3* pFuncLeave3,
        FunctionTailcall3* pFuncTailcall3) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3WithInfo(FunctionEnter3WithInfo* pFuncEnter3WithInfo,
        FunctionLeave3WithInfo* pFuncLeave3WithInfo, FunctionTailcall3WithInfo* pFuncTailcall3WithInfo) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionEnter3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
        COR_PRF_FRAME_INFO* pFrameInfo, ULONG* pcbArgumentInfo, COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionLeave3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
        COR_PRF_FRAME_INFO* pFrameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE* pRetvalRange) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionTailcall3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
        COR_PRF_FRAME_INFO* pFrameInfo) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EnumModules(ICorProfilerModuleEnum** ppEnum) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetRuntimeInformation(USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType,
        USHORT* pMajorVersion, USHORT* pMinorVersion, USHORT* pBuildNumber, USHORT* pQFEVersion, ULONG cchVersionString,
        ULONG* pcchVersionString, WCHAR szVersionString[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetThreadStaticAddress2(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
        ThreadID threadId, void** ppAddress) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetAppDomainsContainingModule(ModuleID moduleId, ULONG32 cAppDomainIds, ULONG32* pcAppDomainIds,
        AppDomainID appDomainIds[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetModuleInfo2(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName,
        WCHAR szName[], AssemblyID* pAssemblyId, DWORD* pdwModuleFlags) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EnumThreads(ICorProfilerThreadEnum** ppEnum) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE InitializeCurrentThread() override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE RequestRevert(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[],
        HRESULT status[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetCodeInfo3(FunctionID functionID, ReJITID reJitId, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
        COR_PRF_CODE_INFO codeInfos[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetFunctionFromIP2(LPCBYTE ip, FunctionID* pFunctionId, ReJITID* pReJitId) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetReJITIDs(FunctionID functionId, ULONG cReJitIds, ULONG* pcReJitIds, ReJITID reJitIds[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetILToNativeMapping2(FunctionID functionId, ReJITID reJitId, ULONG32 cMap, ULONG32* pcMap,
        COR_DEBUG_IL_TO_NATIVE_MAP map[]) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE EnumJITedFunctions2(ICorProfilerFunctionEnum** ppEnum) override
    {
        return E_FAIL;
    }
    HRESULT STDMETHODCALLTYPE GetObjectSize2(ObjectID objectId, SIZE_T* pcSize) override
    {
        return E_FAIL;
    }

};
