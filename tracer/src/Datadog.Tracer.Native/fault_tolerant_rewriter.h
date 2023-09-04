#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_REWRITER_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_REWRITER_H_

#include "cor_profiler.h"
#include "corhlpr.h"

#include <corprof.h>

namespace fault_tolerant
{

class FaultTolerantRewriter : public MethodRewriter
{
private:
    bool is_fault_tolerant_instrumentation_enabled = false;
    std::unique_ptr<MethodRewriter> m_methodRewriter;
    std::shared_ptr<RejitHandler> m_rejit_handler = nullptr;
    std::shared_ptr<RejitWorkOffloader> m_work_offloader;

    HRESULT ApplyKickoffInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);
    static HRESULT ApplyOriginalInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);
    HRESULT InjectSuccessfulInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl, LPCBYTE pMethodBytes) const;
    HRESULT DuplicateMethodOnlyInDotnet3Onward(RejitHandlerModuleMethod* methodHandler, ModuleID moduleId,
                                            mdMethodDef methodId) const;
    HRESULT RewriteInternal(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);

public:
    FaultTolerantRewriter(CorProfiler* corProfiler, std::unique_ptr<MethodRewriter> methodRewriter, std::shared_ptr<RejitWorkOffloader> work_offloader);

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl) override;
    InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    WSTRING GetInstrumentationVersion(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace fault_tolerant

#endif