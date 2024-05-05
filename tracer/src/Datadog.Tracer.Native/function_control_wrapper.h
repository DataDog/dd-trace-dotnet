#pragma once

#include "cor.h"
#include "corprof.h"
#include <atomic>

namespace trace
{
class FunctionControlWrapper : ICorProfilerFunctionControl, ICorProfilerInfo, IUnknown
    {
    protected:
        std::atomic<int> refCount;

        ICorProfilerInfo* m_profilerInfo;
        ModuleID m_moduleId;
        mdMethodDef m_methodId;

        bool m_isDirty;
        LPCBYTE m_originalMethodBody;
        ULONG m_originalBodyLen;
        LPCBYTE m_methodBody;
        ULONG m_bodyLen;
        DWORD m_flags;

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
        HRESULT STDMETHODCALLTYPE GetClassFromObject(ObjectID objectId, ClassID *pClassId);
        HRESULT STDMETHODCALLTYPE GetClassFromToken(ModuleID moduleId, mdTypeDef typeDef, ClassID *pClassId);
        HRESULT STDMETHODCALLTYPE GetCodeInfo(FunctionID functionId, LPCBYTE *pStart, ULONG *pcSize);
        HRESULT STDMETHODCALLTYPE GetEventMask(DWORD *pdwEvents);
        HRESULT STDMETHODCALLTYPE GetFunctionFromIP(LPCBYTE ip, FunctionID *pFunctionId);
        HRESULT STDMETHODCALLTYPE GetFunctionFromToken(ModuleID moduleId, mdToken token, FunctionID *pFunctionId);
        HRESULT STDMETHODCALLTYPE GetHandleFromThread(ThreadID threadId, HANDLE *phThread);
        HRESULT STDMETHODCALLTYPE GetObjectSize(ObjectID objectId, ULONG *pcSize);
        HRESULT STDMETHODCALLTYPE IsArrayClass(ClassID classId, CorElementType *pBaseElemType, ClassID *pBaseClassId, ULONG *pcRank);
        HRESULT STDMETHODCALLTYPE GetThreadInfo(ThreadID threadId, DWORD *pdwWin32ThreadId);
        HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ThreadID *pThreadId);
        HRESULT STDMETHODCALLTYPE GetClassIDInfo(ClassID classId, ModuleID *pModuleId, mdTypeDef *pTypeDefToken);
        HRESULT STDMETHODCALLTYPE GetFunctionInfo(FunctionID functionId, ClassID *pClassId, ModuleID *pModuleId, mdToken *pToken);
        HRESULT STDMETHODCALLTYPE SetEventMask(DWORD dwEvents);
        HRESULT STDMETHODCALLTYPE SetEnterLeaveFunctionHooks(FunctionEnter *pFuncEnter, FunctionLeave *pFuncLeave, FunctionTailcall *pFuncTailcall);
        HRESULT STDMETHODCALLTYPE SetFunctionIDMapper(FunctionIDMapper *pFunc);
        HRESULT STDMETHODCALLTYPE GetTokenAndMetaDataFromFunction(FunctionID functionId, REFIID riid, IUnknown **ppImport, mdToken *pToken);
        HRESULT STDMETHODCALLTYPE GetModuleInfo(ModuleID moduleId, LPCBYTE *ppBaseLoadAddress, ULONG cchName, ULONG *pcchName, WCHAR szName[], AssemblyID *pAssemblyId);
        HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, REFIID riid, IUnknown **ppOut);
        HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE *ppMethodHeader, ULONG *pcbMethodSize);
        HRESULT STDMETHODCALLTYPE GetILFunctionBodyAllocator(ModuleID moduleId, IMethodMalloc **ppMalloc);
        HRESULT STDMETHODCALLTYPE SetILFunctionBody(ModuleID moduleId, mdMethodDef methodid, LPCBYTE pbNewILMethodHeader);
        HRESULT STDMETHODCALLTYPE GetAppDomainInfo(AppDomainID appDomainId, ULONG cchName, ULONG *pcchName, WCHAR szName[], ProcessID *pProcessId);
        HRESULT STDMETHODCALLTYPE GetAssemblyInfo(AssemblyID assemblyId, ULONG cchName, ULONG *pcchName, WCHAR szName[], AppDomainID *pAppDomainId, ModuleID *pModuleId);
        HRESULT STDMETHODCALLTYPE SetFunctionReJIT(FunctionID functionId);
        HRESULT STDMETHODCALLTYPE ForceGC( void);
        HRESULT STDMETHODCALLTYPE SetILInstrumentedCodeMap(FunctionID functionId, BOOL fStartJit, ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[  ]);
        HRESULT STDMETHODCALLTYPE GetInprocInspectionInterface(IUnknown **ppicd);
        HRESULT STDMETHODCALLTYPE GetInprocInspectionIThisThread(IUnknown **ppicd);
        HRESULT STDMETHODCALLTYPE GetThreadContext(ThreadID threadId, ContextID *pContextId);
        HRESULT STDMETHODCALLTYPE BeginInprocDebugging(BOOL fThisThreadOnly, DWORD *pdwProfilerContext);
        HRESULT STDMETHODCALLTYPE EndInprocDebugging(DWORD dwProfilerContext);
        HRESULT STDMETHODCALLTYPE GetILToNativeMapping(FunctionID functionId, ULONG32 cMap, ULONG32 *pcMap, COR_DEBUG_IL_TO_NATIVE_MAP map[  ]);
    };
}

