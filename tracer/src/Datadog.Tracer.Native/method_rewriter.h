#ifndef DD_CLR_PROFILER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_METHOD_REWRITER_H_

#include "../../../shared/src/native-src/util.h"
#include "cor.h"

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

    virtual HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) = 0;

    virtual ~MethodRewriter() = default;
};


class TracerMethodRewriter : public MethodRewriter
{
private:
    ILInstr* CreateFilterForException(ILRewriterWrapper* rewriter, mdTypeRef exception, mdTypeRef type_ref, ULONG exceptionValueIndex);

public:

    TracerMethodRewriter(CorProfiler* corProfiler) : MethodRewriter(corProfiler)
    {
    }

    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace trace

#endif // DD_CLR_PROFILER_METHOD_REWRITER_H_