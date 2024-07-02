#include "tracer_handler_module_method.h"

namespace trace
{

//
// TracerRejitHandlerModuleMethod
//

TracerRejitHandlerModuleMethod::TracerRejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module,
                                                               const FunctionInfo& functionInfo,
                                                               const IntegrationDefinition& integrationDefinition,
                                                               std::unique_ptr<MethodRewriter> methodRewriter) :
    RejitHandlerModuleMethod(methodDef, module, functionInfo, std::move(methodRewriter)),
    m_integrationDefinition(std::make_unique<IntegrationDefinition>(integrationDefinition))
{
}

IntegrationDefinition* TracerRejitHandlerModuleMethod::GetIntegrationDefinition()
{
    return m_integrationDefinition.get();
}

} // namespace trace
