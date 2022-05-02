#include "instrumented_assembly_generator_cor_profiler_info.h"
#include "instrumented_assembly_generator_helper.h"
#include "../log.h"

namespace instrumented_assembly_generator
{
InstrumentedAssemblyGeneratorCorProfilerInfo::InstrumentedAssemblyGeneratorCorProfilerInfo(
    IUnknown* pICorProfilerInfoUnk) :
    m_pICorProfilerInfoUnk(pICorProfilerInfoUnk)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerInfo::.ctor");
    AddRef();
}

InstrumentedAssemblyGeneratorCorProfilerInfo::~InstrumentedAssemblyGeneratorCorProfilerInfo()
{
}

HRESULT STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerInfo::QueryInterface(REFIID riid, void** ppvObject)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerInfo::QueryInterface");
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
        const HRESULT hr = m_pICorProfilerInfoUnk->QueryInterface(riid, reinterpret_cast<void**>(&temp));
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

ULONG STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerInfo::AddRef(void)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerInfo::AddRef");
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerInfo::Release(void)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerInfo::Release");
    int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags,
                                                                        const IID& riid, IUnknown** ppOut)
{
    HRESULT hr;
    ComPtr<IUnknown> temp;
    IfFailRet(m_corProfilerInfo->GetModuleMetaData(moduleId, dwOpenFlags, riid, temp.GetAddressOf()));
    try
    {
        const auto metadataInterfaces =
            new InstrumentedAssemblyGeneratorMetadataInterfaces(temp, m_corProfilerInfo.Get());
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

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid,
                                                                        LPCBYTE pbNewILMethodHeader)
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

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassFromObject(ObjectID objectId, ClassID* pClassId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetClassFromObject(objectId, pClassId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef,
                                                                        ClassID* pClassId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetClassFromToken(moduleId, typeDef, pClassId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetCodeInfo(functionId, pStart, pcSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetEventMask(DWORD* pdwEvents)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetEventMask(pdwEvents);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetFunctionFromIP(ip, pFunctionId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionFromToken(ModuleID moduleId, mdToken token,
                                                                           FunctionID* pFunctionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetFunctionFromToken(moduleId, token, pFunctionId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetHandleFromThread(ThreadID threadId, HANDLE* phThread)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetHandleFromThread(threadId, phThread);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetObjectSize(ObjectID objectId, ULONG* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetObjectSize(objectId, pcSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::IsArrayClass(ClassID classId, CorElementType* pBaseElemType,
                                                                   ClassID* pBaseClassId, ULONG* pcRank)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->IsArrayClass(classId, pBaseElemType, pBaseClassId, pcRank);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetThreadInfo(threadId, pdwWin32ThreadId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetCurrentThreadID(ThreadID* pThreadId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetCurrentThreadID(pThreadId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassIDInfo(ClassID classId, ModuleID* pModuleId,
                                                                     mdTypeDef* pTypeDefToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetClassIDInfo(classId, pModuleId, pTypeDefToken);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionInfo(FunctionID functionId, ClassID* pClassId,
                                                                      ModuleID* pModuleId, mdToken* pToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetFunctionInfo(functionId, pClassId, pModuleId, pToken);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEventMask(DWORD dwEvents)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetEventMask(dwEvents);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter,
                                                                                 FunctionLeave* pFuncLeave,
                                                                                 FunctionTailcall* pFuncTailcall)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->SetEnterLeaveFunctionHooks(pFuncEnter, pFuncLeave, pFuncTailcall);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetFunctionIDMapper(FunctionIDMapper* pFunc)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetFunctionIDMapper(pFunc);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetTokenAndMetaDataFromFunction(FunctionID functionId,
                                                                                      const IID& riid,
                                                                                      IUnknown** ppImport,
                                                                                      mdToken* pToken)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetTokenAndMetaDataFromFunction(functionId, riid, ppImport, pToken);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress,
                                                                    ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                                                    AssemblyID* pAssemblyId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetModuleInfo(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId,
                                                                        LPCBYTE* ppMethodHeader, ULONG* pcbMethodSize)
{
    return m_corProfilerInfo->GetILFunctionBody(moduleId, methodId, ppMethodHeader, pcbMethodSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetILFunctionBodyAllocator(ModuleID moduleId,
                                                                                 IMethodMalloc** ppMalloc)
{
    return m_corProfilerInfo->GetILFunctionBodyAllocator(moduleId, ppMalloc);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName,
                                                                       ULONG* pcchName, WCHAR szName[],
                                                                       ProcessID* pProcessId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetAppDomainInfo(appDomainId, cchName, pcchName, szName, pProcessId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName,
                                                                      ULONG* pcchName, WCHAR szName[],
                                                                      AppDomainID* pAppDomainId, ModuleID* pModuleId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetAssemblyInfo(assemblyId, cchName, pcchName, szName, pAppDomainId, pModuleId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetFunctionReJIT(FunctionID functionId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->SetFunctionReJIT(functionId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::ForceGC()
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->ForceGC();
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit,
                                                                               ULONG cILMapEntries,
                                                                               COR_IL_MAP rgILMapEntries[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->SetILInstrumentedCodeMap(functionId, fStartJit, cILMapEntries, rgILMapEntries);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetInprocInspectionInterface(IUnknown** ppicd)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetInprocInspectionInterface(ppicd);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetInprocInspectionIThisThread(IUnknown** ppicd)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetInprocInspectionIThisThread(ppicd);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetThreadContext(ThreadID threadId, ContextID* pContextId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->GetThreadContext(threadId, pContextId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::BeginInprocDebugging(BOOL fThisThreadOnly,
                                                                           DWORD* pdwProfilerContext)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->BeginInprocDebugging(fThisThreadOnly, pdwProfilerContext);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EndInprocDebugging(DWORD dwProfilerContext)
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))->EndInprocDebugging(dwProfilerContext);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetILToNativeMapping(FunctionID functionId, ULONG32 cMap,
                                                                           ULONG32* pcMap,
                                                                           COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo>(__uuidof(ICorProfilerInfo))
        ->GetILToNativeMapping(functionId, cMap, pcMap, map);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::DoStackSnapshot(ThreadID thread, StackSnapshotCallback* callback,
                                                                      ULONG32 infoFlags, void* clientData,
                                                                      BYTE context[], ULONG32 contextSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->DoStackSnapshot(thread, callback, infoFlags, clientData, context, contextSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEnterLeaveFunctionHooks2(FunctionEnter2* pFuncEnter,
                                                                                  FunctionLeave2* pFuncLeave,
                                                                                  FunctionTailcall2* pFuncTailcall)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->SetEnterLeaveFunctionHooks2(pFuncEnter, pFuncLeave, pFuncTailcall);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionInfo2(FunctionID funcId, COR_PRF_FRAME_INFO frameInfo,
                                                                       ClassID* pClassId, ModuleID* pModuleId,
                                                                       mdToken* pToken, ULONG32 cTypeArgs,
                                                                       ULONG32* pcTypeArgs, ClassID typeArgs[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetFunctionInfo2(funcId, frameInfo, pClassId, pModuleId, pToken, cTypeArgs, pcTypeArgs, typeArgs);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetStringLayout(ULONG* pBufferLengthOffset,
                                                                      ULONG* pStringLengthOffset, ULONG* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetStringLayout(pBufferLengthOffset, pStringLengthOffset, pBufferOffset);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassLayout(ClassID classID, COR_FIELD_OFFSET rFieldOffset[],
                                                                     ULONG cFieldOffset, ULONG* pcFieldOffset,
                                                                     ULONG* pulClassSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassLayout(classID, rFieldOffset, cFieldOffset, pcFieldOffset, pulClassSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassIDInfo2(ClassID classId, ModuleID* pModuleId,
                                                                      mdTypeDef* pTypeDefToken, ClassID* pParentClassId,
                                                                      ULONG32 cNumTypeArgs, ULONG32* pcNumTypeArgs,
                                                                      ClassID typeArgs[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassIDInfo2(classId, pModuleId, pTypeDefToken, pParentClassId, cNumTypeArgs, pcNumTypeArgs, typeArgs);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetCodeInfo2(FunctionID functionID, ULONG32 cCodeInfos,
                                                                   ULONG32* pcCodeInfos, COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetCodeInfo2(functionID, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetClassFromTokenAndTypeArgs(ModuleID moduleID, mdTypeDef typeDef,
                                                                                   ULONG32 cTypeArgs,
                                                                                   ClassID typeArgs[],
                                                                                   ClassID* pClassID)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetClassFromTokenAndTypeArgs(moduleID, typeDef, cTypeArgs, typeArgs, pClassID);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionFromTokenAndTypeArgs(
    ModuleID moduleID, mdMethodDef funcDef, ClassID classId, ULONG32 cTypeArgs, ClassID typeArgs[],
    FunctionID* pFunctionID)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetFunctionFromTokenAndTypeArgs(moduleID, funcDef, classId, cTypeArgs, typeArgs, pFunctionID);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumModuleFrozenObjects(ModuleID moduleID,
                                                                              ICorProfilerObjectEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->EnumModuleFrozenObjects(moduleID, ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetArrayObjectInfo(ObjectID objectId, ULONG32 cDimensions,
                                                                         ULONG32 pDimensionSizes[],
                                                                         int pDimensionLowerBounds[], BYTE** ppData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetArrayObjectInfo(objectId, cDimensions, pDimensionSizes, pDimensionLowerBounds, ppData);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetBoxClassLayout(ClassID classId, ULONG32* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetBoxClassLayout(classId, pBufferOffset);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetThreadAppDomain(ThreadID threadId, AppDomainID* pAppDomainId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetThreadAppDomain(threadId, pAppDomainId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetRVAStaticAddress(ClassID classId, mdFieldDef fieldToken,
                                                                          void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetRVAStaticAddress(classId, fieldToken, ppAddress);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetAppDomainStaticAddress(ClassID classId, mdFieldDef fieldToken,
                                                                                AppDomainID appDomainId,
                                                                                void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetAppDomainStaticAddress(classId, fieldToken, appDomainId, ppAddress);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetThreadStaticAddress(ClassID classId, mdFieldDef fieldToken,
                                                                             ThreadID threadId, void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetThreadStaticAddress(classId, fieldToken, threadId, ppAddress);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetContextStaticAddress(ClassID classId, mdFieldDef fieldToken,
                                                                              ContextID contextId, void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetContextStaticAddress(classId, fieldToken, contextId, ppAddress);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetStaticFieldInfo(ClassID classId, mdFieldDef fieldToken,
                                                                         COR_PRF_STATIC_TYPE* pFieldInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetStaticFieldInfo(classId, fieldToken, pFieldInfo);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetGenerationBounds(ULONG cObjectRanges, ULONG* pcObjectRanges,
                                                                          COR_PRF_GC_GENERATION_RANGE ranges[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))
        ->GetGenerationBounds(cObjectRanges, pcObjectRanges, ranges);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetObjectGeneration(ObjectID objectId,
                                                                          COR_PRF_GC_GENERATION_RANGE* range)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))->GetObjectGeneration(objectId, range);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetNotifiedExceptionClauseInfo(COR_PRF_EX_CLAUSE_INFO* pinfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo2>(__uuidof(ICorProfilerInfo2))->GetNotifiedExceptionClauseInfo(pinfo);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumJITedFunctions(ICorProfilerFunctionEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))->EnumJITedFunctions(ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::RequestProfilerDetach(DWORD dwExpectedCompletionMilliseconds)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->RequestProfilerDetach(dwExpectedCompletionMilliseconds);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetFunctionIDMapper2(FunctionIDMapper2* pFunc, void* clientData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetFunctionIDMapper2(pFunc, clientData);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetStringLayout2(ULONG* pStringLengthOffset, ULONG* pBufferOffset)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetStringLayout2(pStringLengthOffset, pBufferOffset);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEnterLeaveFunctionHooks3(FunctionEnter3* pFuncEnter3,
                                                                                  FunctionLeave3* pFuncLeave3,
                                                                                  FunctionTailcall3* pFuncTailcall3)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetEnterLeaveFunctionHooks3(pFuncEnter3, pFuncLeave3, pFuncTailcall3);
}

HRESULT
InstrumentedAssemblyGeneratorCorProfilerInfo::SetEnterLeaveFunctionHooks3WithInfo(
    FunctionEnter3WithInfo* pFuncEnter3WithInfo, FunctionLeave3WithInfo* pFuncLeave3WithInfo,
    FunctionTailcall3WithInfo* pFuncTailcall3WithInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->SetEnterLeaveFunctionHooks3WithInfo(pFuncEnter3WithInfo, pFuncLeave3WithInfo, pFuncTailcall3WithInfo);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionEnter3Info(
    FunctionID functionId, COR_PRF_ELT_INFO eltInfo, COR_PRF_FRAME_INFO* pFrameInfo, ULONG* pcbArgumentInfo,
    COR_PRF_FUNCTION_ARGUMENT_INFO* pArgumentInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionEnter3Info(functionId, eltInfo, pFrameInfo, pcbArgumentInfo, pArgumentInfo);
}

HRESULT
InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionLeave3Info(FunctionID functionId, COR_PRF_ELT_INFO eltInfo,
                                                                    COR_PRF_FRAME_INFO* pFrameInfo,
                                                                    COR_PRF_FUNCTION_ARGUMENT_RANGE* pRetvalRange)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionLeave3Info(functionId, eltInfo, pFrameInfo, pRetvalRange);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionTailcall3Info(FunctionID functionId,
                                                                               COR_PRF_ELT_INFO eltInfo,
                                                                               COR_PRF_FRAME_INFO* pFrameInfo)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetFunctionTailcall3Info(functionId, eltInfo, pFrameInfo);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumModules(ICorProfilerModuleEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))->EnumModules(ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetRuntimeInformation(
    USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType, USHORT* pMajorVersion, USHORT* pMinorVersion,
    USHORT* pBuildNumber, USHORT* pQFEVersion, ULONG cchVersionString, ULONG* pcchVersionString,
    WCHAR szVersionString[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetRuntimeInformation(pClrInstanceId, pRuntimeType, pMajorVersion, pMinorVersion, pBuildNumber, pQFEVersion,
                                cchVersionString, pcchVersionString, szVersionString);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetThreadStaticAddress2(ClassID classId, mdFieldDef fieldToken,
                                                                              AppDomainID appDomainId,
                                                                              ThreadID threadId, void** ppAddress)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetThreadStaticAddress2(classId, fieldToken, appDomainId, threadId, ppAddress);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetAppDomainsContainingModule(ModuleID moduleId,
                                                                                    ULONG32 cAppDomainIds,
                                                                                    ULONG32* pcAppDomainIds,
                                                                                    AppDomainID appDomainIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetAppDomainsContainingModule(moduleId, cAppDomainIds, pcAppDomainIds, appDomainIds);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetModuleInfo2(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress,
                                                                     ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                                                     AssemblyID* pAssemblyId, DWORD* pdwModuleFlags)
{
    return m_corProfilerInfo.As<ICorProfilerInfo3>(__uuidof(ICorProfilerInfo3))
        ->GetModuleInfo2(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId, pdwModuleFlags);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumThreads(ICorProfilerThreadEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->EnumThreads(ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::InitializeCurrentThread()
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->InitializeCurrentThread();
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::RequestReJIT(ULONG cFunctions, ModuleID moduleIds[],
                                                                   mdMethodDef methodIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->RequestReJIT(cFunctions, moduleIds, methodIds);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::RequestRevert(ULONG cFunctions, ModuleID moduleIds[],
                                                                    mdMethodDef methodIds[], HRESULT status[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->RequestRevert(cFunctions, moduleIds, methodIds, status);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetCodeInfo3(FunctionID functionID, ReJITID reJitId,
                                                                   ULONG32 cCodeInfos, ULONG32* pcCodeInfos,
                                                                   COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetCodeInfo3(functionID, reJitId, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionFromIP2(LPCBYTE ip, FunctionID* pFunctionId,
                                                                         ReJITID* pReJitId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetFunctionFromIP2(ip, pFunctionId, pReJitId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetReJITIDs(FunctionID functionId, ULONG cReJitIds,
                                                                  ULONG* pcReJitIds, ReJITID reJitIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetReJITIDs(functionId, cReJitIds, pcReJitIds, reJitIds);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetILToNativeMapping2(FunctionID functionId, ReJITID reJitId,
                                                                            ULONG32 cMap, ULONG32* pcMap,
                                                                            COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))
        ->GetILToNativeMapping2(functionId, reJitId, cMap, pcMap, map);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumJITedFunctions2(ICorProfilerFunctionEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->EnumJITedFunctions2(ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetObjectSize2(ObjectID objectId, SIZE_T* pcSize)
{
    return m_corProfilerInfo.As<ICorProfilerInfo4>(__uuidof(ICorProfilerInfo4))->GetObjectSize2(objectId, pcSize);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetEventMask2(DWORD* pdwEventsLow, DWORD* pdwEventsHigh)
{
    return m_corProfilerInfo.As<ICorProfilerInfo5>(__uuidof(ICorProfilerInfo5))
        ->GetEventMask2(pdwEventsLow, pdwEventsHigh);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEventMask2(DWORD dwEventsLow, DWORD dwEventsHigh)
{
    return m_corProfilerInfo.As<ICorProfilerInfo5>(__uuidof(ICorProfilerInfo5))
        ->SetEventMask2(dwEventsLow, dwEventsHigh);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumNgenModuleMethodsInliningThisMethod(
    ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
    ICorProfilerMethodEnum** ppEnum)
{
    return m_corProfilerInfo.As<ICorProfilerInfo6>(__uuidof(ICorProfilerInfo6))
        ->EnumNgenModuleMethodsInliningThisMethod(inlinersModuleId, inlineeModuleId, inlineeMethodId, incompleteData,
                                                  ppEnum);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::ApplyMetaData(ModuleID moduleId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))->ApplyMetaData(moduleId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetInMemorySymbolsLength(ModuleID moduleId,
                                                                               DWORD* pCountSymbolBytes)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))
        ->GetInMemorySymbolsLength(moduleId, pCountSymbolBytes);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::ReadInMemorySymbols(ModuleID moduleId, DWORD symbolsReadOffset,
                                                                          BYTE* pSymbolBytes, DWORD countSymbolBytes,
                                                                          DWORD* pCountSymbolBytesRead)
{
    return m_corProfilerInfo.As<ICorProfilerInfo7>(__uuidof(ICorProfilerInfo7))
        ->ReadInMemorySymbols(moduleId, symbolsReadOffset, pSymbolBytes, countSymbolBytes, pCountSymbolBytesRead);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::IsFunctionDynamic(FunctionID functionId, BOOL* isDynamic)
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->IsFunctionDynamic(functionId, isDynamic);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetFunctionFromIP3(LPCBYTE ip, FunctionID* functionId,
                                                                         ReJITID* pReJitId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->GetFunctionFromIP3(ip, functionId, pReJitId);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetDynamicFunctionInfo(FunctionID functionId, ModuleID* moduleId,
                                                                             PCCOR_SIGNATURE* ppvSig, ULONG* pbSig,
                                                                             ULONG cchName, ULONG* pcchName,
                                                                             WCHAR wszName[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo8>(__uuidof(ICorProfilerInfo8))
        ->GetDynamicFunctionInfo(functionId, moduleId, ppvSig, pbSig, cchName, pcchName, wszName);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetNativeCodeStartAddresses(FunctionID functionID,
                                                                                  ReJITID reJitId,
                                                                                  ULONG32 cCodeStartAddresses,
                                                                                  ULONG32* pcCodeStartAddresses,
                                                                                  UINT_PTR codeStartAddresses[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetNativeCodeStartAddresses(functionID, reJitId, cCodeStartAddresses, pcCodeStartAddresses,
                                      codeStartAddresses);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetILToNativeMapping3(UINT_PTR pNativeCodeStartAddress,
                                                                            ULONG32 cMap, ULONG32* pcMap,
                                                                            COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetILToNativeMapping3(pNativeCodeStartAddress, cMap, pcMap, map);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetCodeInfo4(UINT_PTR pNativeCodeStartAddress, ULONG32 cCodeInfos,
                                                                   ULONG32* pcCodeInfos, COR_PRF_CODE_INFO codeInfos[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo9>(__uuidof(ICorProfilerInfo9))
        ->GetCodeInfo4(pNativeCodeStartAddress, cCodeInfos, pcCodeInfos, codeInfos);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EnumerateObjectReferences(ObjectID objectId,
                                                                                ObjectReferenceCallback callback,
                                                                                void* clientData)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->EnumerateObjectReferences(objectId, callback, clientData);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::IsFrozenObject(ObjectID objectId, BOOL* pbFrozen)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->IsFrozenObject(objectId, pbFrozen);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetLOHObjectSizeThreshold(DWORD* pThreshold)
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->GetLOHObjectSizeThreshold(pThreshold);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::RequestReJITWithInliners(DWORD dwRejitFlags, ULONG cFunctions,
                                                                               ModuleID moduleIds[],
                                                                               mdMethodDef methodIds[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))
        ->RequestReJITWithInliners(dwRejitFlags, cFunctions, moduleIds, methodIds);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SuspendRuntime()
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->SuspendRuntime();
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::ResumeRuntime()
{
    return m_corProfilerInfo.As<ICorProfilerInfo10>(__uuidof(ICorProfilerInfo10))->ResumeRuntime();
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::GetEnvironmentVariableW(const WCHAR* szName, ULONG cchValue,
                                                                              ULONG* pcchValue, WCHAR szValue[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo11>(__uuidof(ICorProfilerInfo11))
        ->GetEnvironmentVariableW(szName, cchValue, pcchValue, szValue);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::SetEnvironmentVariableW(const WCHAR* szName, const WCHAR* szValue)
{
    return m_corProfilerInfo.As<ICorProfilerInfo11>(__uuidof(ICorProfilerInfo11))
        ->SetEnvironmentVariableW(szName, szValue);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeStartSession(
    UINT32 cProviderConfigs, COR_PRF_EVENTPIPE_PROVIDER_CONFIG pProviderConfigs[], BOOL requestRundown,
    EVENTPIPE_SESSION* pSession)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeStartSession(cProviderConfigs, pProviderConfigs, requestRundown, pSession);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeAddProviderToSession(
    EVENTPIPE_SESSION session, COR_PRF_EVENTPIPE_PROVIDER_CONFIG providerConfig)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeAddProviderToSession(session, providerConfig);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeStopSession(EVENTPIPE_SESSION session)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))->EventPipeStopSession(session);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeCreateProvider(const WCHAR* providerName,
                                                                              EVENTPIPE_PROVIDER* pProvider)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeCreateProvider(providerName, pProvider);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeGetProviderInfo(EVENTPIPE_PROVIDER provider,
                                                                               ULONG cchName, ULONG* pcchName,
                                                                               WCHAR providerName[])
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeGetProviderInfo(provider, cchName, pcchName, providerName);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeDefineEvent(
    EVENTPIPE_PROVIDER provider, const WCHAR* eventName, UINT32 eventID, UINT64 keywords, UINT32 eventVersion,
    UINT32 level, UINT8 opcode, BOOL needStack, UINT32 cParamDescs, COR_PRF_EVENTPIPE_PARAM_DESC pParamDescs[],
    EVENTPIPE_EVENT* pEvent)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeDefineEvent(provider, eventName, eventID, keywords, eventVersion, level, opcode, needStack,
                               cParamDescs, pParamDescs, pEvent);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerInfo::EventPipeWriteEvent(EVENTPIPE_EVENT event, UINT32 cData,
                                                                          COR_PRF_EVENT_DATA data[],
                                                                          LPCGUID pActivityId,
                                                                          LPCGUID pRelatedActivityId)
{
    return m_corProfilerInfo.As<ICorProfilerInfo12>(__uuidof(ICorProfilerInfo12))
        ->EventPipeWriteEvent(event, cData, data, pActivityId, pRelatedActivityId);
}
} // namespace datadog::shared::nativeloader
