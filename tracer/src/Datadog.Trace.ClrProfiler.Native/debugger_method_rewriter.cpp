#include "debugger_method_rewriter.h"
#include "debugger_rejit_handler_module_method.h"
#include "cor_profiler.h"
#include "il_rewriter_wrapper.h"
#include "logger.h"
#include "stats.h"
#include "version.h"
#include "environment_variables_util.h"

namespace debugger
{

HRESULT DebuggerMethodRewriter::Rewrite(RejitHandlerModule* moduleHandler, RejitHandlerModuleMethod* methodHandler)
{
    return E_NOTIMPL;
}

} // namespace debugger