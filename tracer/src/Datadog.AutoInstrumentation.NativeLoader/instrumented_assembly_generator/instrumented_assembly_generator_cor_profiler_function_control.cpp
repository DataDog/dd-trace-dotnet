#include "instrumented_assembly_generator_cor_profiler_function_control.h"
#include "instrumented_assembly_generator_helper.h"
#include "instrumented_assembly_generator_metadata_interfaces.h"
#include "../log.h"

namespace instrumented_assembly_generator
{
InstrumentedAssemblyGeneratorCorProfilerFunctionControl::InstrumentedAssemblyGeneratorCorProfilerFunctionControl(
    ICorProfilerFunctionControl* corProfilerFunctionControl, ICorProfilerInfo12* corProfilerInfo12, ModuleID moduleId,
    mdMethodDef methodId) :
    m_pICorProfilerFunctionControl(corProfilerFunctionControl),
    m_corProfilerInfo(corProfilerInfo12),
    m_moduleId(moduleId),
    m_methodId(methodId)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerFunctionControl::.ctor");
    AddRef();
}

InstrumentedAssemblyGeneratorCorProfilerFunctionControl::~InstrumentedAssemblyGeneratorCorProfilerFunctionControl()
{
}

HRESULT STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerFunctionControl::QueryInterface(REFIID riid,
                                                                                                  void** ppvObject)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerFunctionControl::QueryInterface");
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == __uuidof(ICorProfilerFunctionControl) || riid == IID_IUnknown)
    {
        *ppvObject = this;
        this->AddRef();
        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerFunctionControl::AddRef(void)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerFunctionControl::AddRef");
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE InstrumentedAssemblyGeneratorCorProfilerFunctionControl::Release(void)
{
    Log::Debug("InstrumentedAssemblyGeneratorCorProfilerFunctionControl::Release");
    const int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerFunctionControl::SetILFunctionBody(ULONG cbNewILMethodHeader,
                                                                                   LPCBYTE pbNewILMethodHeader)
{
    auto hr = m_pICorProfilerFunctionControl->SetILFunctionBody(cbNewILMethodHeader, pbNewILMethodHeader);
    IfFailRet(hr);
    // When we are in rejit, we have the size, so pass it.
    const auto writeHr =
        WriteILChanges(m_moduleId, m_methodId, pbNewILMethodHeader, cbNewILMethodHeader, m_corProfilerInfo);
    if (FAILED(writeHr))
    {
        Log::Error("SetILFunctionBody: fail to write IL to disk");
    }

    return hr;
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerFunctionControl::SetCodegenFlags(DWORD flags)
{
    return m_pICorProfilerFunctionControl->SetCodegenFlags(flags);
}

HRESULT InstrumentedAssemblyGeneratorCorProfilerFunctionControl::SetILInstrumentedCodeMap(ULONG cILMapEntries,
                                                                                          COR_IL_MAP rgILMapEntries[])
{
    return m_pICorProfilerFunctionControl->SetILInstrumentedCodeMap(cILMapEntries, rgILMapEntries);
}
} // namespace datadog::shared::nativeloader
