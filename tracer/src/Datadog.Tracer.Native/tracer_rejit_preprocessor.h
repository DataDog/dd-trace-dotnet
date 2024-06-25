#pragma once

#include "cor.h"
#include "corprof.h"
#include "tracer_integration_definition.h"
#include "module_metadata.h"
#include "rejit_preprocessor.h"
#include <future>


namespace trace
{
class CorProfiler;
class RejitHandler;
class RejitWorkOffloader;
class RejitHandlerModuleMethod;
class RejitHandlerModule;
struct FunctionInfo;

/// <summary>
/// TracerRejitPreprocessor
/// </summary>
class TracerRejitPreprocessor : public RejitPreprocessor<IntegrationDefinition>
{
public:
    using RejitPreprocessor::RejitPreprocessor;

    TracerRejitPreprocessor(CorProfiler* corProfiler, std::shared_ptr<RejitHandler> rejit_handler,
                      std::shared_ptr<RejitWorkOffloader> work_offloader);

protected:
    const MethodReference& GetTargetMethod(const IntegrationDefinition& integrationDefinition) final;
    const bool GetIsDerived(const IntegrationDefinition& definition) final;
    const bool GetIsInterface(const IntegrationDefinition& definition) final;
    const bool GetIsExactSignatureMatch(const IntegrationDefinition& definition) final;
    const bool GetIsEnabled(const IntegrationDefinition& definition) final;
    const bool SupportsSelectiveEnablement() final;

    const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                 const IntegrationDefinition& integrationDefinition) final;
    void UpdateMethod(RejitHandlerModuleMethod* method, const IntegrationDefinition& definition) final;
    bool ShouldSkipModule(const ModuleInfo& moduleInfo, const IntegrationDefinition& integrationDefinition) final;
};

} // namespace trace
