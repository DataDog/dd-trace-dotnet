#include "instrumented_assembly_generator_cor_profiler_info.h"
#include "../log.h"
#include "instrumented_assembly_generator_helper.h"

namespace instrumented_assembly_generator
{
CorProfilerInfo::CorProfilerInfo(IUnknown* pICorProfilerInfoUnk)
{
    m_pICorProfilerInfoUnk.Attach(pICorProfilerInfoUnk);
}

CorProfilerInfo::~CorProfilerInfo()
{
}

HRESULT STDMETHODCALLTYPE CorProfilerInfo::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == __uuidof(ICorProfilerInfo12) || riid == __uuidof(ICorProfilerInfo11) ||
        riid == __uuidof(ICorProfilerInfo10) || riid == __uuidof(ICorProfilerInfo9) ||
        riid == __uuidof(ICorProfilerInfo8) || riid == __uuidof(ICorProfilerInfo7) ||
        riid == __uuidof(ICorProfilerInfo6) || riid == __uuidof(ICorProfilerInfo5) ||
        riid == __uuidof(ICorProfilerInfo4) || riid == __uuidof(ICorProfilerInfo3) ||
        riid == __uuidof(ICorProfilerInfo2) || riid == __uuidof(ICorProfilerInfo) || riid == IID_IUnknown)
    {
        ComPtr<ICorProfilerInfo12> temp;
        const HRESULT hr = m_pICorProfilerInfoUnk->QueryInterface(riid, reinterpret_cast<void**>(temp.GetAddressOf()));
        if (FAILED(hr))
        {
            Log::Warn(
                "InstrumentedAssemblyGeneratorCorProfilerInfo::Ctor: Failed to get interface of ICorProfilerInfoX.");
            return hr;
        }
        if (temp.Get() != nullptr)
        {
            m_corProfilerInfo = temp;
        }

        *ppvObject = this;
        this->AddRef();
        return hr;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CorProfilerInfo::AddRef(void)
{
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE CorProfilerInfo::Release(void)
{
    int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

HRESULT CorProfilerInfo::GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, const IID& riid, IUnknown** ppOut)
{
    HRESULT hr;
    ComPtr<IUnknown> temp;
    IfFailRet(m_corProfilerInfo->GetModuleMetaData(moduleId, dwOpenFlags, riid, temp.GetAddressOf()));
    try
    {
        const auto metadataInterfaces = new MetadataInterfaces(temp);
        *ppOut = reinterpret_cast<IUnknown*>(metadataInterfaces);
    }
    catch (...)
    {
        try
        {
            Log::Error("GetModuleMetaData: failed to create InstrumentedAssemblyGeneratorMetadataInterfaces");
        }
        catch (...)
        {
        }
        *ppOut = temp.Get();
    }

    return S_OK;
}

HRESULT CorProfilerInfo::SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader)
{
    // Order is important here:
    // we must first call `SetILFunctionBody` and only then call `WriteILChanges`,
    // because the latter relies on being able to read the update IL by methodID
    const auto hr = m_corProfilerInfo->SetILFunctionBody(moduleId, methodid, pbNewILMethodHeader);

    if (SUCCEEDED(hr))
    {
        const auto writeHr = WriteILChanges(moduleId, methodid, pbNewILMethodHeader, 0, m_corProfilerInfo.Get());
        if (FAILED(writeHr))
        {
            Log::Error("SetILFunctionBody: fail to write IL to disk");
        }
    }

    return hr;
}

///////////////////////////////////////////////////////////////////
/// Delegate all the remaining functions to the "real" ICorProfilerInfo ///
///////////////////////////////////////////////////////////////////

HRESULT CorProfilerInfo::GetClassFromObject(ObjectID objectId, ClassID* pClassId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetClassFromObject(objectId, pClassId);
}

HRESULT CorProfilerInfo::GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetClassFromToken(moduleId, typeDef, pClassId);
}

HRESULT CorProfilerInfo::GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetCodeInfo(functionId, pStart, pcSize);
}

HRESULT CorProfilerInfo::GetEventMask(DWORD* pdwEvents)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetEventMask(pdwEvents);
}

HRESULT CorProfilerInfo::GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetFunctionFromIP(ip, pFunctionId);
}

HRESULT CorProfilerInfo::GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID* pFunctionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetFunctionFromToken(moduleId, token, pFunctionId);
}

HRESULT CorProfilerInfo::GetHandleFromThread(ThreadID threadId, HANDLE* phThread)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetHandleFromThread(threadId, phThread);
}

HRESULT CorProfilerInfo::GetObjectSize(ObjectID objectId, ULONG* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetObjectSize(objectId, pcSize);
}

HRESULT CorProfilerInfo::IsArrayClass(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId,
                                      ULONG* pcRank)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->IsArrayClass(classId, pBaseElemType, pBaseClassId, pcRank);
}

HRESULT CorProfilerInfo::GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetThreadInfo(threadId, pdwWin32ThreadId);
}

HRESULT CorProfilerInfo::GetCurrentThreadID(ThreadID* pThreadId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetCurrentThreadID(pThreadId);
}

HRESULT CorProfilerInfo::GetClassIDInfo(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetClassIDInfo(classId, pModuleId, pTypeDefToken);
}

HRESULT CorProfilerInfo::GetFunctionInfo(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId, mdToken* pToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetFunctionInfo(functionId, pClassId, pModuleId, pToken);
}

HRESULT CorProfilerInfo::SetEventMask(DWORD dwEvents)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetEventMask(dwEvents);
}

HRESULT CorProfilerInfo::SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave,
                                                    FunctionTailcall* pFuncTailcall)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->SetEnterLeaveFunctionHooks(pFuncEnter, pFuncLeave, pFuncTailcall);
}

HRESULT CorProfilerInfo::SetFunctionIDMapper(FunctionIDMapper* pFunc)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetFunctionIDMapper(pFunc);
}

HRESULT CorProfilerInfo::GetTokenAndMetaDataFromFunction(FunctionID functionId, const IID& riid, IUnknown** ppImport,
                                                         mdToken* pToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetTokenAndMetaDataFromFunction(functionId, riid, ppImport, pToken);
}

HRESULT CorProfilerInfo::GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName,
                                       WCHAR szName[], AssemblyID* pAssemblyId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetModuleInfo(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId);
}

HRESULT CorProfilerInfo::GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader,
                                           ULONG* pcbMethodSize)
{
    return m_corProfilerInfo->GetILFunctionBody(moduleId, methodId, ppMethodHeader, pcbMethodSize);
}

HRESULT CorProfilerInfo::GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc** ppMalloc)
{
    return m_corProfilerInfo->GetILFunctionBodyAllocator(moduleId, ppMalloc);
}

HRESULT CorProfilerInfo::GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                          ProcessID* pProcessId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetAppDomainInfo(appDomainId, cchName, pcchName, szName, pProcessId);
}

HRESULT CorProfilerInfo::GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                         AppDomainID* pAppDomainId, ModuleID* pModuleId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetAssemblyInfo(assemblyId, cchName, pcchName, szName, pAppDomainId, pModuleId);
}

HRESULT CorProfilerInfo::SetFunctionReJIT(FunctionID functionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetFunctionReJIT(functionId);
}

HRESULT CorProfilerInfo::ForceGC()
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->ForceGC();
}

HRESULT CorProfilerInfo::SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries,
                                                  COR_IL_MAP rgILMapEntries[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->SetILInstrumentedCodeMap(functionId, fStartJit, cILMapEntries, rgILMapEntries);
}

HRESULT CorProfilerInfo::GetInprocInspectionInterface(IUnknown** ppicd)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetInprocInspectionInterface(ppicd);
}

HRESULT CorProfilerInfo::GetInprocInspectionIThisThread(IUnknown** ppicd)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetInprocInspectionIThisThread(ppicd);
}

HRESULT CorProfilerInfo::GetThreadContext(ThreadID threadId, ContextID* pContextId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetThreadContext(threadId, pContextId);
}

HRESULT CorProfilerInfo::BeginInprocDebugging(BOOL fThisThreadOnly, DWORD* pdwProfilerContext)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->BeginInprocDebugging(fThisThreadOnly, pdwProfilerContext);
}

HRESULT CorProfilerInfo::EndInprocDebugging(DWORD dwProfilerContext)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->EndInprocDebugging(dwProfilerContext);
}

HRESULT CorProfilerInfo::GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap,
                                              COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetILToNativeMapping(functionId, cMap, pcMap, map);
}

HRESULT CorProfilerInfo::DoStackSnapshot(ThreadID thread, StackSnapshotCallback* callback, ULONG32 infoFlags,
                                         void* clientData, BYTE context[], ULONG32 contextSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->DoStackSnapshot(thread, callback, infoFlags, clientData, context, contextSize);
}

HRESULT CorProfilerInfo::SetEnterLeaveFunctionHooks2(FunctionEnter2* pFuncEnter, FunctionLeave2* pFuncLeave,
                                                     FunctionTailcall2* pFuncTailcall)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->SetEnterLeaveFunctionHooks2(pFuncEnter, pFuncLeave, pFuncTailcall);
}

HRESULT CorProfilerInfo::GetFunctionInfo2(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo, ClassID* pClassId,
                                          ModuleID* pModuleId, mdToken* pToken, ULONG32 cTypeArgs, ULONG32* pcTypeArgs,
                                          ClassID typeArgs[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetFunctionInfo2(funcId, frameInfo, pClassId, pModuleId, pToken, cTypeArgs, pcTypeArgs, typeArgs);
}

HRESULT CorProfilerInfo::GetStringLayout(ULONG* pBufferLengthOffset, ULONG* pStringLengthOffset, ULONG* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetStringLayout(pBufferLengthOffset, pStringLengthOffset, pBufferOffset);
}

HRESULT CorProfilerInfo::GetClassLayout(ClassID classID, COR_FIELD_OFFSET rFieldOffset[], ULONG cFieldOffset,
                                        ULONG* pcFieldOffset, ULONG* pulClassSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassLayout(classID, rFieldOffset, cFieldOffset, pcFieldOffset, pulClassSize);
}

HRESULT CorProfilerInfo::GetClassIDInfo2(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken,
                                         ClassID* pParentClassId, ULONG32 cNumTypeArgs, ULONG32* pcNumTypeArgs,
                                         ClassID typeArgs[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassIDInfo2(classId, pModuleId, pTypeDefToken, pParentClassId, cNumTypeArgs, pcNumTypeArgs, typeArgs);
}

HRESULT CorProfilerInfo::GetCodeInfo2(FunctionID functionID, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                      COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetCodeInfo2(functionID, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT CorProfilerInfo::GetClassFromTokenAndTypeArgs(ModuleID moduleID, mdTypeDef typeDef, ULONG32 cTypeArgs,
                                                      ClassID typeArgs[], ClassID* pClassID)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassFromTokenAndTypeArgs(moduleID, typeDef, cTypeArgs, typeArgs, pClassID);
}

HRESULT CorProfilerInfo::GetFunctionFromTokenAndTypeArgs(ModuleID moduleID, mdMethodDef funcDef, ClassID classId,
                                                         ULONG32 cTypeArgs, ClassID typeArgs[], FunctionID* pFunctionID)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetFunctionFromTokenAndTypeArgs(moduleID, funcDef, classId, cTypeArgs, typeArgs, pFunctionID);
}

HRESULT CorProfilerInfo::EnumModuleFrozenObjects(ModuleID moduleID, ICorProfilerObjectEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->EnumModuleFrozenObjects(moduleID, ppEnum);
}

HRESULT CorProfilerInfo::GetArrayObjectInfo(ObjectID objectId, ULONG32 cDimensions, ULONG32 pDimensionSizes[],
                                            int pDimensionLowerBounds[], BYTE** ppData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetArrayObjectInfo(objectId, cDimensions, pDimensionSizes, pDimensionLowerBounds, ppData);
}

HRESULT CorProfilerInfo::GetBoxClassLayout(ClassID classId, ULONG32* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetBoxClassLayout(classId, pBufferOffset);
}

HRESULT CorProfilerInfo::GetThreadAppDomain(ThreadID threadId, AppDomainID* pAppDomainId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetThreadAppDomain(threadId, pAppDomainId);
}

HRESULT CorProfilerInfo::GetRVAStaticAddress(ClassID classId, mdFieldDef fieldToken, void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetRVAStaticAddress(classId, fieldToken, ppAddress);
}

HRESULT CorProfilerInfo::GetAppDomainStaticAddress(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
                                                   void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetAppDomainStaticAddress(classId, fieldToken, appDomainId, ppAddress);
}

HRESULT CorProfilerInfo::GetThreadStaticAddress(ClassID classId, mdFieldDef fieldToken, ThreadID threadId,
                                                void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetThreadStaticAddress(classId, fieldToken, threadId, ppAddress);
}

HRESULT CorProfilerInfo::GetContextStaticAddress(ClassID classId, mdFieldDef fieldToken, ContextID contextId,
                                                 void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetContextStaticAddress(classId, fieldToken, contextId, ppAddress);
}

HRESULT CorProfilerInfo::GetStaticFieldInfo(ClassID classId, mdFieldDef fieldToken, COR_PRF_STATIC_TYPE* pFieldInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetStaticFieldInfo(classId, fieldToken, pFieldInfo);
}

HRESULT CorProfilerInfo::GetGenerationBounds(ULONG cObjectRanges, ULONG* pcObjectRanges,
                                             COR_PRF_GC_GENERATION_RANGE ranges[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetGenerationBounds(cObjectRanges, pcObjectRanges, ranges);
}

HRESULT CorProfilerInfo::GetObjectGeneration(ObjectID objectId, COR_PRF_GC_GENERATION_RANGE* range)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))->GetObjectGeneration(objectId, range);
}

HRESULT CorProfilerInfo::GetNotifiedExceptionClauseInfo(COR_PRF_EX_CLAUSE_INFO* pinfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))->GetNotifiedExceptionClauseInfo(pinfo);
}

HRESULT CorProfilerInfo::EnumJITedFunctions(ICorProfilerFunctionEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))->EnumJITedFunctions(ppEnum);
}

HRESULT CorProfilerInfo::RequestProfilerDetach(DWORD dwExpectedCompletionMilliseconds)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->RequestProfilerDetach(dwExpectedCompletionMilliseconds);
}

HRESULT CorProfilerInfo::SetFunctionIDMapper2(FunctionIDMapper2* pFunc, void* clientData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetFunctionIDMapper2(pFunc, clientData);
}

HRESULT CorProfilerInfo::GetStringLayout2(ULONG* pStringLengthOffset, ULONG* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetStringLayout2(pStringLengthOffset, pBufferOffset);
}

HRESULT CorProfilerInfo::SetEnterLeaveFunctionHooks3(FunctionEnter3* pFuncEnter3, FunctionLeave3* pFuncLeave3,
                                                     FunctionTailcall3* pFuncTailcall3)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetEnterLeaveFunctionHooks3(pFuncEnter3, pFuncLeave3, pFuncTailcall3);
}

HRESULT
CorProfilerInfo::SetEnterLeaveFunctionHooks3WithInfo(FunctionEnter3WithInfo* pFuncEnter3WithInfo,
                                                     FunctionLeave3WithInfo* pFuncLeave3WithInfo,
                                                     FunctionTailcall3WithInfo* pFuncTailcall3WithInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetEnterLeaveFunctionHooks3WithInfo(pFuncEnter3WithInfo, pFuncLeave3WithInfo, pFuncTailcall3WithInfo);
}

HRESULT CorProfilerInfo::GetFunctionEnter3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                               COR_PRF_FRAME_INFO* pFrameInfo, ULONG* pcbArgumentInfo,
                                               COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionEnter3Info(functionId, eltInfo, pFrameInfo, pcbArgumentInfo, pArgumentInfo);
}

HRESULT
CorProfilerInfo::GetFunctionLeave3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO* pFrameInfo,
                                       COR_PRF_FUNCTION_ARGUMENT_RANGE* pRetvalRange)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionLeave3Info(functionId, eltInfo, pFrameInfo, pRetvalRange);
}

HRESULT CorProfilerInfo::GetFunctionTailcall3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                                  COR_PRF_FRAME_INFO* pFrameInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionTailcall3Info(functionId, eltInfo, pFrameInfo);
}

HRESULT CorProfilerInfo::EnumModules(ICorProfilerModuleEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))->EnumModules(ppEnum);
}

HRESULT CorProfilerInfo::GetRuntimeInformation(USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType,
                                               USHORT* pMajorVersion, USHORT* pMinorVersion, USHORT* pBuildNumber,
                                               USHORT* pQFEVersion, ULONG cchVersionString, ULONG* pcchVersionString,
                                               WCHAR szVersionString[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetRuntimeInformation(pClrInstanceId, pRuntimeType, pMajorVersion, pMinorVersion, pBuildNumber, pQFEVersion,
                                cchVersionString, pcchVersionString, szVersionString);
}

HRESULT CorProfilerInfo::GetThreadStaticAddress2(ClassID classId, mdFieldDef fieldToken, AppDomainID appDomainId,
                                                 ThreadID threadId, void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetThreadStaticAddress2(classId, fieldToken, appDomainId, threadId, ppAddress);
}

HRESULT CorProfilerInfo::GetAppDomainsContainingModule(ModuleID moduleId, ULONG32 cAppDomainIds,
                                                       ULONG32* pcAppDomainIds, AppDomainID appDomainIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetAppDomainsContainingModule(moduleId, cAppDomainIds, pcAppDomainIds, appDomainIds);
}

HRESULT CorProfilerInfo::GetModuleInfo2(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName,
                                        WCHAR szName[], AssemblyID* pAssemblyId, DWORD* pdwModuleFlags)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetModuleInfo2(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId, pdwModuleFlags);
}

HRESULT CorProfilerInfo::EnumThreads(ICorProfilerThreadEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->EnumThreads(ppEnum);
}

HRESULT CorProfilerInfo::InitializeCurrentThread()
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->InitializeCurrentThread();
}

HRESULT CorProfilerInfo::RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->RequestReJIT(cFunctions, moduleIds, methodIds);
}

HRESULT CorProfilerInfo::RequestRevert(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[],
                                       HRESULT status[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->RequestRevert(cFunctions, moduleIds, methodIds, status);
}

HRESULT CorProfilerInfo::GetCodeInfo3(FunctionID functionID, ReJITID reJitId, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                      COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetCodeInfo3(functionID, reJitId, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT CorProfilerInfo::GetFunctionFromIP2(LPCBYTE ip, FunctionID* pFunctionId, ReJITID* pReJitId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetFunctionFromIP2(ip, pFunctionId, pReJitId);
}

HRESULT CorProfilerInfo::GetReJITIDs(FunctionID functionId, ULONG cReJitIds, ULONG* pcReJitIds, ReJITID reJitIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetReJITIDs(functionId, cReJitIds, pcReJitIds, reJitIds);
}

HRESULT CorProfilerInfo::GetILToNativeMapping2(FunctionID functionId, ReJITID reJitId, ULONG32 cMap, ULONG32* pcMap,
                                               COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetILToNativeMapping2(functionId, reJitId, cMap, pcMap, map);
}

HRESULT CorProfilerInfo::EnumJITedFunctions2(ICorProfilerFunctionEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->EnumJITedFunctions2(ppEnum);
}

HRESULT CorProfilerInfo::GetObjectSize2(ObjectID objectId, SIZE_T* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->GetObjectSize2(objectId, pcSize);
}

HRESULT CorProfilerInfo::GetEventMask2(DWORD* pdwEventsLow, DWORD* pdwEventsHigh)
{
    return m_corProfilerInfo.As<ICorProfilerInfo5>(__uuidof(ICorProfilerInfo5))
        ->GetEventMask2(pdwEventsLow, pdwEventsHigh);
}

HRESULT CorProfilerInfo::SetEventMask2(DWORD dwEventsLow, DWORD dwEventsHigh)
{
    return m_corProfilerInfo.As<ICorProfilerInfo5>(__uuidof(ICorProfilerInfo5))
        ->SetEventMask2(dwEventsLow, dwEventsHigh);
}

HRESULT CorProfilerInfo::EnumNgenModuleMethodsInliningThisMethod(ModuleID inlinersModuleId, ModuleID inlineeModuleId,
                                                                 mdMethodDef inlineeMethodId, BOOL* incompleteData,
                                                                 ICorProfilerMethodEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo6>(__uuidof(ICorProfilerInfo6))
        ->EnumNgenModuleMethodsInliningThisMethod(inlinersModuleId, inlineeModuleId, inlineeMethodId, incompleteData,
                                                  ppEnum);
}

HRESULT CorProfilerInfo::ApplyMetaData(ModuleID moduleId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))->ApplyMetaData(moduleId);
}

HRESULT CorProfilerInfo::GetInMemorySymbolsLength(ModuleID moduleId, DWORD* pCountSymbolBytes)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))
        ->GetInMemorySymbolsLength(moduleId, pCountSymbolBytes);
}

HRESULT CorProfilerInfo::ReadInMemorySymbols(ModuleID moduleId, DWORD symbolsReadOffset, BYTE* pSymbolBytes,
                                             DWORD countSymbolBytes, DWORD* pCountSymbolBytesRead)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))
        ->ReadInMemorySymbols(moduleId, symbolsReadOffset, pSymbolBytes, countSymbolBytes, pCountSymbolBytesRead);
}

HRESULT CorProfilerInfo::IsFunctionDynamic(FunctionID functionId, BOOL* isDynamic)
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->IsFunctionDynamic(functionId, isDynamic);
}

HRESULT CorProfilerInfo::GetFunctionFromIP3(LPCBYTE ip, FunctionID* functionId, ReJITID* pReJitId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->GetFunctionFromIP3(ip, functionId, pReJitId);
}

HRESULT CorProfilerInfo::GetDynamicFunctionInfo(FunctionID functionId, ModuleID* moduleId, PCCOR_SIGNATURE* ppvSig,
                                                ULONG* pbSig, ULONG cchName, ULONG* pcchName, WCHAR wszName[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->GetDynamicFunctionInfo(functionId, moduleId, ppvSig, pbSig, cchName, pcchName, wszName);
}

HRESULT CorProfilerInfo::GetNativeCodeStartAddresses(FunctionID functionID, ReJITID reJitId,
                                                     ULONG32 cCodeStartAddresses, ULONG32* pcCodeStartAddresses,
                                                     UINT_PTR codeStartAddresses[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetNativeCodeStartAddresses(functionID, reJitId, cCodeStartAddresses, pcCodeStartAddresses,
                                      codeStartAddresses);
}

HRESULT CorProfilerInfo::GetILToNativeMapping3(UINT_PTR pNativeCodeStartAddress, ULONG32 cMap, ULONG32* pcMap,
                                               COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetILToNativeMapping3(pNativeCodeStartAddress, cMap, pcMap, map);
}

HRESULT CorProfilerInfo::GetCodeInfo4(UINT_PTR pNativeCodeStartAddress, ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                      COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetCodeInfo4(pNativeCodeStartAddress, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT CorProfilerInfo::EnumerateObjectReferences(ObjectID objectId, ObjectReferenceCallback callback,
                                                   void* clientData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->EnumerateObjectReferences(objectId, callback, clientData);
}

HRESULT CorProfilerInfo::IsFrozenObject(ObjectID objectId, BOOL* pbFrozen)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->IsFrozenObject(objectId, pbFrozen);
}

HRESULT CorProfilerInfo::GetLOHObjectSizeThreshold(DWORD* pThreshold)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->GetLOHObjectSizeThreshold(pThreshold);
}

HRESULT CorProfilerInfo::RequestReJITWithInliners(DWORD dwRejitFlags, ULONG cFunctions, ModuleID moduleIds[],
                                                  mdMethodDef methodIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->RequestReJITWithInliners(dwRejitFlags, cFunctions, moduleIds, methodIds);
}

HRESULT CorProfilerInfo::SuspendRuntime()
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->SuspendRuntime();
}

HRESULT CorProfilerInfo::ResumeRuntime()
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->ResumeRuntime();
}

HRESULT CorProfilerInfo::GetEnvironmentVariableW(const WCHAR* szName, ULONG cchValue, ULONG* pcchValue, WCHAR szValue[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo11>(__uuidof(ICorProfilerInfo11))
        ->GetEnvironmentVariableW(szName, cchValue, pcchValue, szValue);
}

HRESULT CorProfilerInfo::SetEnvironmentVariableW(const WCHAR* szName, const WCHAR* szValue)
{
    return m_corProfilerInfo.As<ICorProfilerInfo11>(__uuidof(ICorProfilerInfo11))
        ->SetEnvironmentVariableW(szName, szValue);
}

HRESULT CorProfilerInfo::EventPipeStartSession(UINT32 cProviderConfigs,
                                               COR_PRF_EVENTPIPE_PROVIDER_CONFIG pProviderConfigs[],
                                               BOOL requestRundown, EVENTPIPE_SESSION* pSession)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeStartSession(cProviderConfigs, pProviderConfigs, requestRundown, pSession);
}

HRESULT CorProfilerInfo::EventPipeAddProviderToSession(EVENTPIPE_SESSION session,
                                                       COR_PRF_EVENTPIPE_PROVIDER_CONFIG providerConfig)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeAddProviderToSession(session, providerConfig);
}

HRESULT CorProfilerInfo::EventPipeStopSession(EVENTPIPE_SESSION session)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))->EventPipeStopSession(session);
}

HRESULT CorProfilerInfo::EventPipeCreateProvider(const WCHAR* providerName, EVENTPIPE_PROVIDER* pProvider)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeCreateProvider(providerName, pProvider);
}

HRESULT CorProfilerInfo::EventPipeGetProviderInfo(EVENTPIPE_PROVIDER provider, ULONG cchName, ULONG* pcchName,
                                                  WCHAR providerName[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeGetProviderInfo(provider, cchName, pcchName, providerName);
}

HRESULT CorProfilerInfo::EventPipeDefineEvent(EVENTPIPE_PROVIDER provider, const WCHAR* eventName, UINT32 eventID,
                                              UINT64 keywords, UINT32 eventVersion, UINT32 level, UINT8 opcode,
                                              BOOL needStack, UINT32 cParamDescs,
                                              COR_PRF_EVENTPIPE_PARAM_DESC pParamDescs[], EVENTPIPE_EVENT* pEvent)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeDefineEvent(provider, eventName, eventID, keywords, eventVersion, level, opcode, needStack,
                               cParamDescs, pParamDescs, pEvent);
}

HRESULT CorProfilerInfo::EventPipeWriteEvent(EVENTPIPE_EVENT event, UINT32 cData, COR_PRF_EVENT_DATA data[],
                                             LPCGUID pActivityId, LPCGUID pRelatedActivityId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeWriteEvent(event, cData, data, pActivityId, pRelatedActivityId);
}
} // namespace instrumented_assembly_generator
