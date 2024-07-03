#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_COR_PROFILER_FUNCTION_CONTROL_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_COR_PROFILER_FUNCTION_CONTROL_H_

#include "cor_profiler.h"
#include "../../../../../shared/src/native-src/com_ptr.h"
#include <atomic>
#include <corhlpr.h>
#include <corprof.h>
#include <memory>

namespace fault_tolerant
{

using InjectSuccessfulInstrumentationLambda = std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*, ICorProfilerFunctionControl*, LPCBYTE pMethodBytes)>;

class FaultTolerantCorProfilerFunctionControl : public ICorProfilerFunctionControl
{
private:
    std::atomic<int> m_refCount;
    ICorProfilerFunctionControl* m_pICorProfilerFunctionControl;
    std::shared_ptr<ICorProfilerInfo12> m_corProfilerInfo;
    ModuleID m_moduleId;
    mdMethodDef m_methodId;

    RejitHandlerModule* moduleHandler;
    RejitHandlerModuleMethod* methodHandler;

    InjectSuccessfulInstrumentationLambda injectSuccessfulInstrumentation;

public:
    FaultTolerantCorProfilerFunctionControl(ICorProfilerFunctionControl* corProfilerFunctionControl, ModuleID moduleId,
                                            mdMethodDef methodId, RejitHandlerModule* moduleHandler,
                                            RejitHandlerModuleMethod* methodHandler,
                                            InjectSuccessfulInstrumentationLambda injectSuccessfulInstrumentation);

    ~FaultTolerantCorProfilerFunctionControl();

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    HRESULT __stdcall SetCodegenFlags(DWORD flags) override;
    HRESULT __stdcall SetILFunctionBody(ULONG cbNewILMethodHeader, LPCBYTE pbNewILMethodHeader) override;
    HRESULT __stdcall SetILInstrumentedCodeMap(ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[]) override;
};

}

#endif