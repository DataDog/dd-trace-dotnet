#ifndef DD_CLR_PROFILER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_METHOD_REWRITER_H_

#include "util.h"
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


class TracerIntegrationMethodRewriter : public MethodRewriter, public Singleton<TracerIntegrationMethodRewriter>
{
    friend class Singleton<TracerIntegrationMethodRewriter>;
    
private:
    TracerIntegrationMethodRewriter(){}

public:
    virtual HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) final;
};

} // namespace trace

#endif // DD_CLR_PROFILER_METHOD_REWRITER_H_