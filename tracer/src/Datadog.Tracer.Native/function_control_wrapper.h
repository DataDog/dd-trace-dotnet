#pragma once

#include "cor.h"
#include "corprof.h"
#include <atomic>

namespace trace
{
class FunctionControlWrapper : ICorProfilerFunctionControl, ICorProfilerInfo
    {
    protected:
        std::atomic<int> refCount;

        ICorProfilerInfo* m_profilerInfo;
        ModuleID m_moduleId;
        mdMethodDef m_methodId;

        bool m_isDirty;
        LPCBYTE m_methodBody;
        ULONG m_bodyLen;

    public:
        FunctionControlWrapper(ICorProfilerInfo* profilerInfo, const ModuleID moduleId, const mdMethodDef methodId);
        virtual ~FunctionControlWrapper();

    public:
        inline bool IsDirty() { return m_isDirty; }
        inline ModuleID GetModuleId() { return m_moduleId; }
        inline mdMethodDef GetMethodId() { return m_methodId; }
        inline HRESULT GetMethodBody(LPCBYTE* methodBody, ULONG* bodyLen)
        {
            *methodBody = m_methodBody;
            *bodyLen = m_bodyLen;
            return m_isDirty ? S_OK : S_FALSE;
        }
        HRESULT ApplyChanges(ICorProfilerFunctionControl* pFunctionControl);

    public:
        ULONG STDMETHODCALLTYPE AddRef(void) override;
        ULONG STDMETHODCALLTYPE Release(void) override;
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;


    public: // ICorProfilerFunctionControl
        HRESULT STDMETHODCALLTYPE SetCodegenFlags(DWORD flags) override;
        HRESULT STDMETHODCALLTYPE SetILFunctionBody(ULONG cbNewILMethodHeader, LPCBYTE pbNewILMethodHeader) override;
        HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[]) override;

    public: // ICorProfilerInfo
        HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID* pClassId) override;
        HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID* pClassId) override;
        HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID functionId, LPCBYTE* pStart, ULONG* pcSize) override;
        HRESULT STDMETHODCALLTYPE GetEventMask(DWORD* pdwEvents) override;
        HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE ip, FunctionID* pFunctionId) override;
        HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID moduleId, mdToken token,
                                                       FunctionID* pFunctionId) override;
        HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID threadId, HANDLE* phThread) override;
        HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID objectId, ULONG* pcSize) override;
        HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID classId, CorElementType* pBaseElemType, ClassID* pBaseClassId,
                                               ULONG* pcRank) override;
        HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD* pdwWin32ThreadId) override;
        HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID* pThreadId) override;
        HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID classId, ModuleID* pModuleId,
                                                 mdTypeDef* pTypeDefToken) override;
        HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID functionId, ClassID* pClassId, ModuleID* pModuleId,
                                                  mdToken* pToken) override;
        HRESULT STDMETHODCALLTYPE SetEventMask(DWORD dwEvents) override;
        HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter* pFuncEnter, FunctionLeave* pFuncLeave,
                                                             FunctionTailcall* pFuncTailcall) override;
        HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(FunctionIDMapper* pFunc) override;
        HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID functionId, REFIID riid,
                                                                  IUnknown** ppImport, mdToken* pToken) override;
        HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID moduleId, LPCBYTE* ppBaseLoadAddress, ULONG cchName,
                                                ULONG* pcchName, WCHAR szName[], AssemblyID* pAssemblyId) override;
        HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, REFIID riid,
                                                    IUnknown** ppOut) override;
        HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader,
                                                    ULONG* pcbMethodSize) override;
        HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc** ppMalloc) override;
        HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid,
                                                    LPCBYTE pbNewILMethodHeader) override;
        HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG* pcchName,
                                                   WCHAR szName[], ProcessID* pProcessId) override;
        HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG* pcchName, WCHAR szName[],
                                                  AppDomainID* pAppDomainId, ModuleID* pModuleId) override;
        HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID functionId) override;
        HRESULT STDMETHODCALLTYPE ForceGC(void) override;
        HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries,
                                                           COR_IL_MAP rgILMapEntries[]) override;
        HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown** ppicd) override;
        HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown** ppicd) override;
        HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID threadId, ContextID* pContextId) override;
        HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL fThisThreadOnly, DWORD* pdwProfilerContext) override;
        HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD dwProfilerContext) override;
        HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32* pcMap,
                                                       COR_DEBUG_IL_TO_NATIVE_MAP map[]) override;
    };
}

