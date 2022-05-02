#ifndef DD_CLR_PROFILER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_METHOD_REWRITER_H_

#include "../../../shared/src/native-src/util.h"
#include "cor.h"

namespace trace
{
    // forward declarations
    class RejitHandlerModule;
    class RejitHandlerModuleMethod;

class MethodRewriter
{
public:
    virtual HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) = 0;
};


class TracerMethodRewriter : public MethodRewriter, public shared::Singleton<TracerMethodRewriter>
{
    friend class shared::Singleton<TracerMethodRewriter>;

private:
    TracerMethodRewriter(){}

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace trace

#endif // DD_CLR_PROFILER_METHOD_REWRITER_H_