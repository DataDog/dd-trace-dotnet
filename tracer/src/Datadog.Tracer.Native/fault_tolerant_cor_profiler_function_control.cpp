#include "fault_tolerant_cor_profiler_function_control.h"

// #include "instrumented_assembly_generator_helper.h"
// #include "instrumented_assembly_generator_metadata_interfaces.h"
#include <utility>

using namespace fault_tolerant;

FaultTolerantCorProfilerFunctionControl::FaultTolerantCorProfilerFunctionControl(
    ICorProfilerFunctionControl* corProfilerFunctionControl, ICorProfilerInfo* corProfilerInfo, ModuleID moduleId,
    mdMethodDef methodId,
    RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
    InjectSuccessfulInstrumentationLambda injectSuccessfulInstrumentation) :
    m_moduleId(moduleId),
    m_methodId(methodId),
    moduleHandler(moduleHandler),
    methodHandler(methodHandler),
    injectSuccessfulInstrumentation(std::move(injectSuccessfulInstrumentation)),
    m_pICorProfilerFunctionControl(corProfilerFunctionControl),
    m_pCorProfilerInfo(corProfilerInfo)
{
}

FaultTolerantCorProfilerFunctionControl::~FaultTolerantCorProfilerFunctionControl()
{
}

HRESULT STDMETHODCALLTYPE FaultTolerantCorProfilerFunctionControl::QueryInterface(REFIID riid, void** ppvObject)
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

ULONG STDMETHODCALLTYPE FaultTolerantCorProfilerFunctionControl::AddRef()
{
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE FaultTolerantCorProfilerFunctionControl::Release()
{
    const int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;

    if (count <= 0)
    {
        delete this;
    }

    return count;
}

HRESULT FaultTolerantCorProfilerFunctionControl::SetILFunctionBody(ULONG cbNewILMethodHeader,
                                                                   LPCBYTE pbNewILMethodHeader)
{
    return injectSuccessfulInstrumentation(moduleHandler, methodHandler, m_pICorProfilerFunctionControl,
                                           m_pCorProfilerInfo, pbNewILMethodHeader);
}

HRESULT FaultTolerantCorProfilerFunctionControl::SetCodegenFlags(DWORD flags)
{
    return m_pICorProfilerFunctionControl->SetCodegenFlags(flags);
}

HRESULT FaultTolerantCorProfilerFunctionControl::SetILInstrumentedCodeMap(ULONG cILMapEntries,
                                                                          COR_IL_MAP rgILMapEntries[])
{
    return m_pICorProfilerFunctionControl->SetILInstrumentedCodeMap(cILMapEntries, rgILMapEntries);
}