#ifndef DD_CLR_PROFILER_FAULT_TOLERANT_REWRITER_H_
#define DD_CLR_PROFILER_FAULT_TOLERANT_REWRITER_H_

#include "clr_helpers.h"
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
    std::shared_ptr<RejitWorkOffloader> m_work_offloader = nullptr;

    HRESULT ApplyKickoffInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);
    static HRESULT ApplyOriginalInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);
    HRESULT InjectSuccessfulInstrumentation(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl) const;
    HRESULT RewriteInternal(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl);

    void RequestRejit(std::vector<MethodIdentifier>& rejitRequests, bool enqueueInSameThread = false);
    void RequestRevert(std::vector<MethodIdentifier>& revertRequests, bool enqueueInSameThread = false);

    void EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests, std::shared_ptr<std::promise<void>> promise);
    void EnqueueRequestRevert(std::vector<MethodIdentifier>& revertRequests, std::shared_ptr<std::promise<void>> promise);

public:
    FaultTolerantRewriter(CorProfiler* corProfiler, std::unique_ptr<MethodRewriter> methodRewriter,
                          std::shared_ptr<RejitHandler> rejit_handler,
                          std::shared_ptr<RejitWorkOffloader> work_offloader);

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler, ICorProfilerFunctionControl* pFunctionControl) override;
    InstrumentingProduct GetInstrumentingProduct(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
    WSTRING GetInstrumentationVersion(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace fault_tolerant

#endif