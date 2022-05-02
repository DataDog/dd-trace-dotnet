#pragma once
#include <atomic>
#include <corhlpr.h>
#include <corprof.h>

namespace instrumented_assembly_generator
{
class InstrumentedAssemblyGeneratorCorProfilerFunctionControl : public ICorProfilerFunctionControl
{
private:
    std::atomic<int> m_refCount;
    ICorProfilerFunctionControl* m_pICorProfilerFunctionControl;
    ICorProfilerInfo12* m_corProfilerInfo;
    ModuleID m_moduleId;
    mdMethodDef m_methodId;

public:
    explicit InstrumentedAssemblyGeneratorCorProfilerFunctionControl(ICorProfilerFunctionControl* corProfilerFunctionControl,
                                                   ICorProfilerInfo12* corProfilerInfo12, ModuleID moduleId,
                                                   mdMethodDef methodId);
    ~InstrumentedAssemblyGeneratorCorProfilerFunctionControl();

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef(void) override;
    ULONG STDMETHODCALLTYPE Release(void) override;
    HRESULT __stdcall SetCodegenFlags(DWORD flags) override;
    HRESULT __stdcall SetILFunctionBody(ULONG cbNewILMethodHeader, LPCBYTE pbNewILMethodHeader) override;
    HRESULT __stdcall SetILInstrumentedCodeMap(ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[]) override;
};
} // namespace datadog::shared::nativeloader