#ifndef DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_

#include "method_rewriter.h"
#include "util.h"

using namespace trace;

namespace debugger
{

class DebuggerMethodRewriter : public MethodRewriter, public Singleton<DebuggerMethodRewriter>
{
    friend class Singleton<DebuggerMethodRewriter>;

private:
    DebuggerMethodRewriter(){}

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace debugger

#endif