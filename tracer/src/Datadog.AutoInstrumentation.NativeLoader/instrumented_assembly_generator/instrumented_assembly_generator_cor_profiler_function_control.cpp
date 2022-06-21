#include "instrumented_assembly_generator_cor_profiler_function_control.h"

#include "../log.h"
#include "instrumented_assembly_generator_helper.h"
#include "instrumented_assembly_generator_metadata_interfaces.h"
#include <utility>

namespace instrumented_assembly_generator
{
CorProfilerFunctionControl::CorProfilerFunctionControl(ICorProfilerFunctionControl* corProfilerFunctionControl,
                                                       std::shared_ptr<ICorProfilerInfo12> corProfilerInfo12,
                                                       ModuleID moduleId, mdMethodDef methodId) :
    m_corProfilerInfo(std::move(corProfilerInfo12)),
    m_moduleId(moduleId),
    m_methodId(methodId)
{
    m_pICorProfilerFunctionControl.Attach(corProfilerFunctionControl);
}

CorProfilerFunctionControl::~CorProfilerFunctionControl()
{
}

HRESULT STDMETHODCALLTYPE CorProfilerFunctionControl::QueryInterface(REFIID riid, void** ppvObject)
{
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

ULONG STDMETHODCALLTYPE CorProfilerFunctionControl::AddRef(void)
{
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE CorProfilerFunctionControl::Release(void)
{
    const int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

HRESULT CorProfilerFunctionControl::SetILFunctionBody(ULONG cbNewILMethodHeader, LPCBYTE pbNewILMethodHeader)
{
    auto hr = m_pICorProfilerFunctionControl->SetILFunctionBody(cbNewILMethodHeader, pbNewILMethodHeader);
    IfFailRet(hr);
    // When we are in rejit, we have the size, so pass it.
    const auto writeHr =
        WriteILChanges(m_moduleId, m_methodId, pbNewILMethodHeader, cbNewILMethodHeader, m_corProfilerInfo.get());
    if (FAILED(writeHr))
    {
        Log::Error("SetILFunctionBody: fail to write IL to disk");
    }

    return hr;
}

HRESULT CorProfilerFunctionControl::SetCodegenFlags(DWORD flags)
{
    return m_pICorProfilerFunctionControl->SetCodegenFlags(flags);
}

HRESULT CorProfilerFunctionControl::SetILInstrumentedCodeMap(ULONG cILMapEntries, COR_IL_MAP rgILMapEntries[])
{
    return m_pICorProfilerFunctionControl->SetILInstrumentedCodeMap(cILMapEntries, rgILMapEntries);
}
} // namespace instrumented_assembly_generator
