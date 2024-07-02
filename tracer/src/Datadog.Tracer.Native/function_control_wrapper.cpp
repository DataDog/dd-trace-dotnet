#include "function_control_wrapper.h"

namespace trace
{
FunctionControlWrapper::FunctionControlWrapper(ICorProfilerInfo* profilerInfo, const ModuleID moduleId,
                                               const mdMethodDef methodId) :
    m_profilerInfo(profilerInfo),
    refCount(1),
    m_moduleId(moduleId),
    m_methodId(methodId),
    m_methodBody(nullptr),
    m_bodyLen(0),
    m_isDirty(false)
{
}

FunctionControlWrapper::~FunctionControlWrapper()
{
    if (m_methodBody)
    {
        delete[] m_methodBody;
        m_methodBody = nullptr;
    }
}

HRESULT FunctionControlWrapper::ApplyChanges(ICorProfilerFunctionControl* pFunctionControl)
{
    if (m_isDirty)
    {
        return pFunctionControl->SetILFunctionBody(m_bodyLen, m_methodBody);
    }

    return S_FALSE;
}


HRESULT FunctionControlWrapper::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }
    *ppvObject = nullptr;

    if (riid == __uuidof(IUnknown))
    {
        AddRef();
        *ppvObject = (ICorProfilerFunctionControl*) this;
        return S_OK;
    }
    else if (riid == __uuidof(ICorProfilerFunctionControl))
    {
        AddRef();
        *ppvObject = (ICorProfilerFunctionControl*) this;
        return S_OK;
    }
    else if (riid == __uuidof(ICorProfilerInfo))
    {
        AddRef();
        *ppvObject = (ICorProfilerInfo*) this;
        return S_OK;
    }
    return E_NOINTERFACE;
}
ULONG FunctionControlWrapper::AddRef()
{
    return std::atomic_fetch_add(&this->refCount, 1) + 1;
}
ULONG FunctionControlWrapper::Release()
{
    int count = std::atomic_fetch_sub(&this->refCount, 1) - 1;
    if (count == 0)
    {
        delete this;
    }
    return count;
}

HRESULT FunctionControlWrapper::SetCodegenFlags(DWORD flags)
{
    return S_OK;
}
HRESULT FunctionControlWrapper::SetILFunctionBody(ULONG bodyLen, LPCBYTE pNewIL)
{
    if (bodyLen == 0)
    {
        return E_INVALIDARG;
    }

    if (pNewIL != m_methodBody)
    {
        m_isDirty = true;
        if (m_methodBody)
        {
            delete[] m_methodBody;
        }
        m_bodyLen = bodyLen;
        m_methodBody = new BYTE[m_bodyLen];
        memcpy((void*)m_methodBody, pNewIL, m_bodyLen);
        return S_OK;
    }

    return S_FALSE;
}
HRESULT FunctionControlWrapper::SetILInstrumentedCodeMap(ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[])
{
    return S_OK;
}

// -----------
HRESULT FunctionControlWrapper::GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader, ULONG* pcbMethodSize)
{
    if (moduleId != m_moduleId || methodId != m_methodId)
    {
        return E_INVALIDARG;
    }

    HRESULT hr = S_OK;

    if (!m_methodBody)
    {
        return m_profilerInfo->GetILFunctionBody(moduleId, methodId, ppMethodHeader, pcbMethodSize);
    }

    if (ppMethodHeader)
    {
        *ppMethodHeader = m_methodBody;
    }

    if (pcbMethodSize)
    {
        *pcbMethodSize = m_bodyLen;
    }

    return S_OK;
}

HRESULT FunctionControlWrapper::GetClassFromObject(ObjectID objectId, ClassID* pClassId)
{
    return m_profilerInfo->GetClassFromObject(objectId, pClassId);
}
HRESULT FunctionControlWrapper::GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId)
{
    return m_profilerInfo->GetClassFromToken(moduleId, typeDef, pClassId);
}
HRESULT FunctionControlWrapper::GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize)
{
    return m_profilerInfo->GetCodeInfo(functionId, pStart, pcSize);
}
HRESULT FunctionControlWrapper::GetEventMask(DWORD* pdwEvents)
{
    return m_profilerInfo->GetEventMask(pdwEvents);
}
HRESULT FunctionControlWrapper::GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId)
{
    return m_profilerInfo->GetFunctionFromIP(ip, pFunctionId);
}
HRESULT FunctionControlWrapper::GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID* pFunctionId)
{
    return m_profilerInfo->GetFunctionFromToken(moduleId, token, pFunctionId);
}
HRESULT FunctionControlWrapper::GetHandleFromThread(ThreadID threadId, HANDLE* phThread)
{
    return m_profilerInfo->GetHandleFromThread(threadId, phThread);
}
HRESULT FunctionControlWrapper::GetObjectSize(ObjectID objectId, ULONG* pcSize)
{
    return m_profilerInfo->GetObjectSize(objectId, pcSize);
}
HRESULT FunctionControlWrapper::IsArrayClass(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId, ULONG* pcRank)
{
    return m_profilerInfo->IsArrayClass(classId, pBaseElemType, pBaseClassId, pcRank);
}
HRESULT FunctionControlWrapper::GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId)
{
    return m_profilerInfo->GetThreadInfo(threadId, pdwWin32ThreadId);
}
HRESULT FunctionControlWrapper::GetCurrentThreadID(ThreadID* pThreadId)
{
    return m_profilerInfo->GetCurrentThreadID(pThreadId);
}
HRESULT FunctionControlWrapper::GetClassIDInfo(ClassID classId, ModuleID* pModuleId, mdTypeDef* pTypeDefToken)
{
    return m_profilerInfo->GetClassIDInfo(classId, pModuleId, pTypeDefToken);
}
HRESULT FunctionControlWrapper::GetFunctionInfo(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId, mdToken* pToken)
{
    return m_profilerInfo->GetFunctionInfo(functionId, pClassId, pModuleId, pToken);
}
HRESULT FunctionControlWrapper::SetEventMask(DWORD dwEvents)
{
    return m_profilerInfo->SetEventMask(dwEvents);
}
HRESULT FunctionControlWrapper::SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave, FunctionTailcall* pFuncTailcall)
{
    return m_profilerInfo->SetEnterLeaveFunctionHooks(pFuncEnter, pFuncLeave, pFuncTailcall);
}
HRESULT FunctionControlWrapper::SetFunctionIDMapper(FunctionIDMapper* pFunc)
{
    return m_profilerInfo->SetFunctionIDMapper(pFunc);
}
HRESULT FunctionControlWrapper::GetTokenAndMetaDataFromFunction(FunctionID functionId, REFIID riid, IUnknown** ppImport, mdToken* pToken)
{
    return m_profilerInfo->GetTokenAndMetaDataFromFunction(functionId, riid, ppImport, pToken);
}
HRESULT FunctionControlWrapper::GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName, ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId)
{
    return m_profilerInfo->GetModuleInfo(moduleId, ppBaseLoadAddress, cchName, pcchName, szName, pAssemblyId);
}
HRESULT FunctionControlWrapper::GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, REFIID riid, IUnknown** ppOut)
{
    return m_profilerInfo->GetModuleMetaData(moduleId, dwOpenFlags, riid, ppOut);
}
HRESULT FunctionControlWrapper::GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc** ppMalloc)
{
    return m_profilerInfo->GetILFunctionBodyAllocator(moduleId, ppMalloc);
}
HRESULT FunctionControlWrapper::SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader)
{
    return m_profilerInfo->SetILFunctionBody(moduleId, methodid, pbNewILMethodHeader);
}
HRESULT FunctionControlWrapper::GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName, WCHAR szName[], ProcessID* pProcessId)
{
    return m_profilerInfo->GetAppDomainInfo(appDomainId, cchName, pcchName, szName, pProcessId);
}
HRESULT FunctionControlWrapper::GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[], AppDomainID* pAppDomainId, ModuleID* pModuleId)
{
    return m_profilerInfo->GetAssemblyInfo(assemblyId, cchName, pcchName, szName, pAppDomainId, pModuleId);
}
HRESULT FunctionControlWrapper::SetFunctionReJIT(FunctionID functionId)
{
    return m_profilerInfo->SetFunctionReJIT(functionId);
}
HRESULT FunctionControlWrapper::ForceGC()
{
    return m_profilerInfo->ForceGC();
}
HRESULT FunctionControlWrapper::SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[])
{
    return m_profilerInfo->SetILInstrumentedCodeMap(functionId, fStartJit, cILMapEntries, rgILMapEntries);
}
HRESULT FunctionControlWrapper::GetInprocInspectionInterface(IUnknown** ppicd)
{
    return m_profilerInfo->GetInprocInspectionInterface(ppicd);
}
HRESULT FunctionControlWrapper::GetInprocInspectionIThisThread(IUnknown** ppicd)
{
    return m_profilerInfo->GetInprocInspectionIThisThread(ppicd);
}
HRESULT FunctionControlWrapper::GetThreadContext(ThreadID threadId, ContextID* pContextId)
{
    return m_profilerInfo->GetThreadContext(threadId, pContextId);
}
HRESULT FunctionControlWrapper::BeginInprocDebugging(BOOL fThisThreadOnly, DWORD* pdwProfilerContext)
{
    return m_profilerInfo->BeginInprocDebugging(fThisThreadOnly, pdwProfilerContext);
}
HRESULT FunctionControlWrapper::EndInprocDebugging(DWORD dwProfilerContext)
{
    return m_profilerInfo->EndInprocDebugging(dwProfilerContext);
}
HRESULT FunctionControlWrapper::GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[])
{
    return m_profilerInfo->GetILToNativeMapping(functionId, cMap, pcMap, map);
}

} // namespace trace