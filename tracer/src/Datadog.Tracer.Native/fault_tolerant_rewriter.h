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

    HRESULT ApplyKickoffInstrumentation(RejitHandlerModule* moduleHandler,
                                        RejitHandlerModuleMethod* methodHandler) const;
    static HRESULT ApplyOriginalInstrumentation(RejitHandlerModule* moduleHandler,
                                                RejitHandlerModuleMethod* methodHandler);
    HRESULT RewriteInternal(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) const;

public:
    FaultTolerantRewriter(CorProfiler* corProfiler, std::unique_ptr<MethodRewriter> methodRewriter);

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace fault_tolerant

#endif