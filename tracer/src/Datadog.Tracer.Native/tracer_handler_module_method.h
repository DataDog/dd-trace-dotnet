#pragma once

#include <atomic>
#include <future>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "rejit_handler.h"
#include "method_rewriter.h"
#include "module_metadata.h"

namespace trace
{

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class TracerRejitHandlerModuleMethod : public RejitHandlerModuleMethod
{
private:
    std::unique_ptr<IntegrationDefinition> m_integrationDefinition;

public:
    TracerRejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                                   const IntegrationDefinition& integrationDefinition,
                                   std::unique_ptr<MethodRewriter> methodRewriter);

    IntegrationDefinition* GetIntegrationDefinition();
};

} // namespace trace