#include "tracer_rejit_preprocessor.h"
#include "stats.h"
#include "integration.h"
#include "logger.h"
#include "debugger_members.h"
#include "fault_tolerant_tracker.h"
#include "tracer_handler_module_method.h"
#include "tracer_method_rewriter.h"

namespace trace
{
// TracerRejitPreprocessor
TracerRejitPreprocessor::TracerRejitPreprocessor(CorProfiler* corProfiler, std::shared_ptr<RejitHandler> rejit_handler,
                                                 std::shared_ptr<RejitWorkOffloader> work_offloader) : 
    RejitPreprocessor(corProfiler, rejit_handler, work_offloader, RejitterPriority::Normal)
{
}

const MethodReference& TracerRejitPreprocessor::GetTargetMethod(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.target_method;
}

const bool TracerRejitPreprocessor::GetIsDerived(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.is_derived;
}

const bool TracerRejitPreprocessor::GetIsInterface(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.is_interface;
}

const bool TracerRejitPreprocessor::GetIsExactSignatureMatch(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.is_exact_signature_match;
}

const bool TracerRejitPreprocessor::GetIsEnabled(const IntegrationDefinition& integrationDefinition)
{
    return integrationDefinition.GetEnabled();
}

const bool TracerRejitPreprocessor::SupportsSelectiveEnablement()
{
    return true;
}

const std::unique_ptr<RejitHandlerModuleMethod>
TracerRejitPreprocessor::CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module,
                                      const FunctionInfo& functionInfo,
                                      const IntegrationDefinition& integrationDefinition)
{
    return std::make_unique<TracerRejitHandlerModuleMethod>(methodDef, module, functionInfo, integrationDefinition,
                                                            std::make_unique<TracerMethodRewriter>(m_corProfiler));
}

void TracerRejitPreprocessor::UpdateMethod(RejitHandlerModuleMethod* method, const IntegrationDefinition& definition)
{
    auto tracerMethodHandler = static_cast<TracerRejitHandlerModuleMethod*>(method);
    if (tracerMethodHandler != nullptr)
    {
        tracerMethodHandler->GetIntegrationDefinition()->Update(definition);
    }
}

bool TracerRejitPreprocessor::ShouldSkipModule(const ModuleInfo& moduleInfo,
                                               const IntegrationDefinition& integrationDefinition)
{
    // If the integration is not for the current assembly we skip.

    const auto target_method = GetTargetMethod(integrationDefinition);

    return target_method.type.assembly.name != tracemethodintegration_assemblyname &&
           target_method.type.assembly.name != moduleInfo.assembly.name;
}

} // namespace trace