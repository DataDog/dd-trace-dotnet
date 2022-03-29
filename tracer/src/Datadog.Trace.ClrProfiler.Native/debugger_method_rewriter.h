#ifndef DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_
#define DD_CLR_PROFILER_DEBUGGER_METHOD_REWRITER_H_

#include "method_rewriter.h"
#include "../../../shared/src/native-src/util.h"

using namespace trace;

namespace debugger
{

class DebuggerMethodRewriter : public MethodRewriter, public shared::Singleton<DebuggerMethodRewriter>
{
    friend class shared::Singleton<DebuggerMethodRewriter>;

private:
    DebuggerMethodRewriter(){}

public:
    HRESULT Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler) override;
};

} // namespace debugger

#endif