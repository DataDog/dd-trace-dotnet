#ifndef DD_CLR_PROFILER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_METHOD_REWRITER_H_

#include "../../../shared/src/native-src/util.h"
#include "cor.h"
#include "instrumenting_product.h"
#include "module_metadata.h"

struct ILInstr;
class ILRewriterWrapper;

namespace trace
{
    // forward declarations
    class RejitHandlerModule;
    class RejitHandlerModuleMethod;
    class CorProfiler;

class MethodRewriter
{
protected:
    CorProfiler* m_corProfiler;

public:
    MethodRewriter(CorProfiler* corProfiler)
        : m_corProfiler(corProfiler)
    {        
    }

    virtual HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler,
                            ICorProfilerFunctionControl* pFunctionControl, ICorProfilerInfo* corProfilerInfo) = 0;
    virtual InstrumentingProducts GetInstrumentingProduct(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) = 0;
    virtual WSTRING GetInstrumentationId(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) = 0;

    virtual ~MethodRewriter() = default;
};

} // namespace trace

#endif // DD_CLR_PROFILER_METHOD_REWRITER_H_