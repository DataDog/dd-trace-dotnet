#pragma once
#include <atomic>
#include <corhlpr.h>
#include <corprof.h>
#include "instrumented_assembly_generator_metadata_interfaces.h"

namespace instrumented_assembly_generator
{
class CorProfilerInfo : public ICorProfilerInfo12
{
private:
    std::atomic<int> m_refCount;
    ComPtr<ICorProfilerInfo12> m_corProfilerInfo;
    ComPtr<IUnknown> m_pICorProfilerInfoUnk;

public:
    CorProfilerInfo(IUnknown* pICorProfilerInfoUnk);
    ~CorProfilerInfo();

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef(void) override;
    ULONG STDMETHODCALLTYPE Release(void) override;
    HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID* pClassId) override;
    HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId) override;
    HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize) override;
    HRESULT STDMETHODCALLTYPE GetEventMask(DWORD* pdwEvents) override;
    HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId) override;
    HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID* pFunctionId) override;
    HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID threadId, HANDLE* phThread) override;
    HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID objectId, ULONG* pcSize) override;
    HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId,
                                           ULONG* pcRank) override;
    HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId) override;
    HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID* pThreadId) override;
    HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken) override;
    HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId,
                                              mdToken* pToken) override;
    HRESULT STDMETHODCALLTYPE SetEventMask(DWORD dwEvents) override;
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave,
                                                         FunctionTailcall* pFuncTailcall) override;
    HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(FunctionIDMapper* pFunc) override;
    HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID functionId, const IID& riid,
                                                              IUnknown** ppImport, mdToken* pToken) override;
    HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName,
                                            ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId) override;
    HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, const IID& riid,
                                                IUnknown** ppOut) override;
    HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader,
                                                ULONG* pcbMethodSize) override;
    HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc** ppMalloc) override;
    HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid,
                                                LPCBYTE pbNewILMethodHeader) override;
    HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                               ProcessID* pProcessId) override;
    HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                              AppDomainID* pAppDomainId, ModuleID* pModuleId) override;
    HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID functionId) override;
    HRESULT STDMETHODCALLTYPE ForceGC() override;
    HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries,
                                                       COR_IL_MAP rgILMapEntries[]) override;
    HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown** ppicd) override;
    HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown** ppicd) override;
    HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID threadId, ContextID* pContextId) override;
    HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL fThisThreadOnly, DWORD* pdwProfilerContext) override;
    HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD dwProfilerContext) override;
    HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap,
                                                   COR_DEBUG_IL_TO_NATIVE_MAP map[]) override;
    HRESULT STDMETHODCALLTYPE DoStackSnapshot(ThreadID thread, StackSnapshotCallback* callback, ULONG32 infoFlags,
                                              void* clientData, BYTE context[], ULONG32 contextSize) override;
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks2(FunctionEnter2* pFuncEnter, FunctionLeave2* pFuncLeave,
                                                          FunctionTailcall2* pFuncTailcall) override;
    HRESULT STDMETHODCALLTYPE GetFunctionInfo2(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo, ClassID* pClassId,
                                               ModuleID* pModuleId, mdToken* pToken, ULONG32 cTypeArgs,
                                               ULONG32* pcTypeArgs, ClassID typeArgs[]) override;
    HRESULT STDMETHODCALLTYPE GetStringLayout(ULONG* pBufferLengthOffset, ULONG* pStringLengthOffset,
                                              ULONG* pBufferOffset) override;
    HRESULT STDMETHODCALLTYPE GetClassLayout(ClassID classID, COR_FIELD_OFFSET rFieldOffset[], ULONG cFieldOffset,
                                             ULONG* pcFieldOffset, ULONG* pulClassSize) override;
    HRESULT STDMETHODCALLTYPE GetClassIDInfo2(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken,
                                              ClassID* pParentClassId, ULONG32 cNumTypeArgs, ULONG32* pcNumTypeArgs,
                                              ClassID typeArgs[]) override;
    HRESULT STDMETHODCALLTYPE GetCodeInfo2(FunctionID functionID, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                           COR_PRF_CODE_INFO codeInfos[]) override;
    HRESULT STDMETHODCALLTYPE GetClassFromTokenAndTypeArgs(ModuleID moduleID, mdTypeDef typeDef, ULONG32 cTypeArgs,
                                                           ClassID typeArgs[], ClassID* pClassID) override;
    HRESULT STDMETHODCALLTYPE GetFunctionFromTokenAndTypeArgs(ModuleID moduleID, mdMethodDef funcDef, ClassID classId,
                                                              ULONG32 cTypeArgs, ClassID typeArgs[],
                                                              FunctionID* pFunctionID) override;
    HRESULT STDMETHODCALLTYPE EnumModuleFrozenObjects(ModuleID moduleID, ICorProfilerObjectEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE GetArrayObjectInfo(ObjectID objectId, ULONG32 cDimensions, ULONG32 pDimensionSizes[],
                                                 int pDimensionLowerBounds[], BYTE** ppData) override;
    HRESULT STDMETHODCALLTYPE GetBoxClassLayout(ClassID classId, ULONG32* pBufferOffset) override;
    HRESULT STDMETHODCALLTYPE GetThreadAppDomain(ThreadID threadId, AppDomainID* pAppDomainId) override;
    HRESULT STDMETHODCALLTYPE GetRVAStaticAddress(ClassID classId, mdFieldDef fieldToken, void** ppAddress) override;
    HRESULT STDMETHODCALLTYPE GetAppDomainStaticAddress(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
                                                        void** ppAddress) override;
    HRESULT STDMETHODCALLTYPE GetThreadStaticAddress(ClassID classId, mdFieldDef fieldToken, ThreadID threadId,
                                                     void** ppAddress) override;
    HRESULT STDMETHODCALLTYPE GetContextStaticAddress(ClassID classId, mdFieldDef fieldToken, ContextID contextId,
                                                      void** ppAddress) override;
    HRESULT STDMETHODCALLTYPE GetStaticFieldInfo(ClassID classId, mdFieldDef fieldToken,
                                                 COR_PRF_STATIC_TYPE* pFieldInfo) override;
    HRESULT STDMETHODCALLTYPE GetGenerationBounds(ULONG cObjectRanges, ULONG* pcObjectRanges,
                                                  COR_PRF_GC_GENERATION_RANGE ranges[]) override;
    HRESULT STDMETHODCALLTYPE GetObjectGeneration(ObjectID objectId, COR_PRF_GC_GENERATION_RANGE* range) override;
    HRESULT STDMETHODCALLTYPE GetNotifiedExceptionClauseInfo(COR_PRF_EX_CLAUSE_INFO* pinfo) override;
    HRESULT STDMETHODCALLTYPE EnumJITedFunctions(ICorProfilerFunctionEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE RequestProfilerDetach(DWORD dwExpectedCompletionMilliseconds) override;
    HRESULT STDMETHODCALLTYPE SetFunctionIDMapper2(FunctionIDMapper2* pFunc, void* clientData) override;
    HRESULT STDMETHODCALLTYPE GetStringLayout2(ULONG* pStringLengthOffset, ULONG* pBufferOffset) override;
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3(FunctionEnter3* pFuncEnter3, FunctionLeave3* pFuncLeave3,
                                                          FunctionTailcall3* pFuncTailcall3) override;
    HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks3WithInfo(
        FunctionEnter3WithInfo* pFuncEnter3WithInfo, FunctionLeave3WithInfo* pFuncLeave3WithInfo,
        FunctionTailcall3WithInfo* pFuncTailcall3WithInfo) override;
    HRESULT STDMETHODCALLTYPE GetFunctionEnter3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                                    COR_PRF_FRAME_INFO* pFrameInfo, ULONG* pcbArgumentInfo,
                                                    COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo) override;
    HRESULT STDMETHODCALLTYPE GetFunctionLeave3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                                    COR_PRF_FRAME_INFO* pFrameInfo,
                                                    COR_PRF_FUNCTION_ARGUMENT_RANGE* pRetvalRange) override;
    HRESULT STDMETHODCALLTYPE GetFunctionTailcall3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                                       COR_PRF_FRAME_INFO* pFrameInfo) override;
    HRESULT STDMETHODCALLTYPE EnumModules(ICorProfilerModuleEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE GetRuntimeInformation(USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType,
                                                    USHORT* pMajorVersion, USHORT* pMinorVersion, USHORT* pBuildNumber,
                                                    USHORT* pQFEVersion, ULONG cchVersionString,
                                                    ULONG* pcchVersionString, WCHAR szVersionString[]) override;
    HRESULT STDMETHODCALLTYPE GetThreadStaticAddress2(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
                                                      ThreadID threadId, void** ppAddress) override;
    HRESULT STDMETHODCALLTYPE GetAppDomainsContainingModule(ModuleID moduleId, ULONG32 cAppDomainIds,
                                                            ULONG32* pcAppDomainIds,
                                                            AppDomainID appDomainIds[]) override;
    HRESULT STDMETHODCALLTYPE GetModuleInfo2(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName,
                                             ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId,
                                             DWORD* pdwModuleFlags) override;
    HRESULT STDMETHODCALLTYPE EnumThreads(ICorProfilerThreadEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE InitializeCurrentThread() override;
    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override;
    HRESULT STDMETHODCALLTYPE RequestRevert(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[],
                                            HRESULT status[]) override;
    HRESULT STDMETHODCALLTYPE GetCodeInfo3(FunctionID functionID, ReJITID reJitId, ULONG32 cCodeInfos,
                                           ULONG32* pcCodeInfos, COR_PRF_CODE_INFO codeInfos[]) override;
    HRESULT STDMETHODCALLTYPE GetFunctionFromIP2(LPCBYTE ip, FunctionID* pFunctionId, ReJITID* pReJitId) override;
    HRESULT STDMETHODCALLTYPE GetReJITIDs(FunctionID functionId, ULONG cReJitIds, ULONG* pcReJitIds,
                                          ReJITID reJitIds[]) override;
    HRESULT STDMETHODCALLTYPE GetILToNativeMapping2(FunctionID functionId, ReJITID reJitId, ULONG32 cMap,
                                                    ULONG32* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[]) override;
    HRESULT STDMETHODCALLTYPE EnumJITedFunctions2(ICorProfilerFunctionEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE GetObjectSize2(ObjectID objectId, SIZE_T* pcSize) override;
    HRESULT STDMETHODCALLTYPE GetEventMask2(DWORD* pdwEventsLow, DWORD* pdwEventsHigh) override;
    HRESULT STDMETHODCALLTYPE SetEventMask2(DWORD dwEventsLow, DWORD dwEventsHigh) override;
    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(ModuleID inlinersModuleId,
                                                                      ModuleID inlineeModuleId,
                                                                      mdMethodDef inlineeMethodId, BOOL* incompleteData,
                                                                      ICorProfilerMethodEnum** ppEnum) override;
    HRESULT STDMETHODCALLTYPE ApplyMetaData(ModuleID moduleId) override;
    HRESULT STDMETHODCALLTYPE GetInMemorySymbolsLength(ModuleID moduleId, DWORD* pCountSymbolBytes) override;
    HRESULT STDMETHODCALLTYPE ReadInMemorySymbols(ModuleID moduleId, DWORD symbolsReadOffset, BYTE* pSymbolBytes,
                                                  DWORD countSymbolBytes, DWORD* pCountSymbolBytesRead) override;
    HRESULT STDMETHODCALLTYPE IsFunctionDynamic(FunctionID functionId, BOOL* isDynamic) override;
    HRESULT STDMETHODCALLTYPE GetFunctionFromIP3(LPCBYTE ip, FunctionID* functionId, ReJITID* pReJitId) override;
    HRESULT STDMETHODCALLTYPE GetDynamicFunctionInfo(FunctionID functionId, ModuleID* moduleId, PCCOR_SIGNATURE* ppvSig,
                                                     ULONG* pbSig, ULONG cchName, ULONG* pcchName,
                                                     WCHAR wszName[]) override;
    HRESULT STDMETHODCALLTYPE GetNativeCodeStartAddresses(FunctionID functionID, ReJITID reJitId,
                                                          ULONG32 cCodeStartAddresses, ULONG32* pcCodeStartAddresses,
                                                          UINT_PTR codeStartAddresses[]) override;
    HRESULT STDMETHODCALLTYPE GetILToNativeMapping3(UINT_PTR pNativeCodeStartAddress, ULONG32 cMap, ULONG32* pcMap,
                                                    COR_DEBUG_IL_TO_NATIVE_MAP map[]) override;
    HRESULT STDMETHODCALLTYPE GetCodeInfo4(UINT_PTR pNativeCodeStartAddress, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                           COR_PRF_CODE_INFO codeInfos[]) override;
    HRESULT STDMETHODCALLTYPE EnumerateObjectReferences(ObjectID objectId, ObjectReferenceCallback callback,
                                                        void* clientData) override;
    HRESULT STDMETHODCALLTYPE IsFrozenObject(ObjectID objectId, BOOL* pbFrozen) override;
    HRESULT STDMETHODCALLTYPE GetLOHObjectSizeThreshold(DWORD* pThreshold) override;
    HRESULT STDMETHODCALLTYPE RequestReJITWithInliners(DWORD dwRejitFlags, ULONG cFunctions, ModuleID moduleIds[],
                                                       mdMethodDef methodIds[]) override;
    HRESULT STDMETHODCALLTYPE SuspendRuntime() override;
    HRESULT STDMETHODCALLTYPE ResumeRuntime() override;
    HRESULT STDMETHODCALLTYPE GetEnvironmentVariableW(const WCHAR* szName, ULONG cchValue, ULONG* pcchValue,
                                                      WCHAR szValue[]) override;
    HRESULT STDMETHODCALLTYPE SetEnvironmentVariableW(const WCHAR* szName, const WCHAR* szValue) override;
    HRESULT STDMETHODCALLTYPE EventPipeStartSession(UINT32 cProviderConfigs,
                                                    COR_PRF_EVENTPIPE_PROVIDER_CONFIG pProviderConfigs[],
                                                    BOOL requestRundown, EVENTPIPE_SESSION* pSession) override;
    HRESULT STDMETHODCALLTYPE EventPipeAddProviderToSession(EVENTPIPE_SESSION session,
                                                            COR_PRF_EVENTPIPE_PROVIDER_CONFIG providerConfig) override;
    HRESULT STDMETHODCALLTYPE EventPipeStopSession(EVENTPIPE_SESSION session) override;
    HRESULT STDMETHODCALLTYPE EventPipeCreateProvider(const WCHAR* providerName,
                                                      EVENTPIPE_PROVIDER* pProvider) override;
    HRESULT STDMETHODCALLTYPE EventPipeGetProviderInfo(EVENTPIPE_PROVIDER provider, ULONG cchName, ULONG* pcchName,
                                                       WCHAR providerName[]) override;
    HRESULT STDMETHODCALLTYPE EventPipeDefineEvent(EVENTPIPE_PROVIDER provider, const WCHAR* eventName, UINT32 eventID,
                                                   UINT64 keywords, UINT32 eventVersion, UINT32 level, UINT8 opcode,
                                                   BOOL needStack, UINT32 cParamDescs,
                                                   COR_PRF_EVENTPIPE_PARAM_DESC pParamDescs[],
                                                   EVENTPIPE_EVENT* pEvent) override;
    HRESULT STDMETHODCALLTYPE EventPipeWriteEvent(EVENTPIPE_EVENT event, UINT32 cData, COR_PRF_EVENT_DATA data[],
                                                  LPCGUID pActivityId, LPCGUID pRelatedActivityId) override;
};

} // namespace datadog::shared::nativeloader
