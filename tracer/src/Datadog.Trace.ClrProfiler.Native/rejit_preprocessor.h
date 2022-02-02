#ifndef DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_
#define DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_

#include "integration.h"
#include <future>
#include "cor.h"
#include "corprof.h"
#include "module_metadata.h"

namespace trace
{
class RejitHandler;
class RejitWorkOffloader;
class RejitHandlerModuleMethod;
class RejitHandlerModule;
struct FunctionInfo;

/// <summary>
/// Responsible to determine what are the methods that should be rejitted and prepares the metadata needed in the rewriting process.
/// </summary>
template <class RejitRequestDefinition>
class RejitPreprocessor
{
private:
    void ProcessTypeDefForRejit(const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
                           ComPtr<IMetaDataEmit2>& metadataEmit, ComPtr<IMetaDataAssemblyImport>& assemblyImport,
                           ComPtr<IMetaDataAssemblyEmit>& assemblyEmit, const ModuleInfo& moduleInfo,
                           const mdTypeDef typeDef, std::vector<ModuleID>& vtModules,
                           std::vector<mdMethodDef>& vtMethodDefs);

protected:
    std::shared_ptr<RejitHandler> m_rejit_handler = nullptr;
    std::shared_ptr<RejitWorkOffloader> m_work_offloader = nullptr;

    virtual const MethodReference& GetTargetMethod(const RejitRequestDefinition& definition) = 0;
    virtual const bool GetIsDerived(const RejitRequestDefinition& definition) = 0;
    virtual const std::unique_ptr<RejitHandlerModuleMethod> CreateMethod(const mdMethodDef methodDef,
                                                                         RejitHandlerModule* module,
                                                                         const FunctionInfo& functionInfo,
                                                                         const RejitRequestDefinition& definition) = 0;

public:
    RejitPreprocessor(std::shared_ptr<RejitHandler> rejit_handler, std::shared_ptr<RejitWorkOffloader> work_offloader);

    ULONG RequestRejitForLoadedModules(const std::vector<ModuleID>& modules,
                                       const std::vector<RejitRequestDefinition>& requests,
                                       bool enqueueInSameThread = false);

    void EnqueueRequestRejitForLoadedModules(const std::vector<ModuleID>& modulesVector,
                                             const std::vector<RejitRequestDefinition>& requests,
                                             std::promise<ULONG>* promise);
};

/// <summary>
/// TracerRejitPreprocessor
/// </summary>
class TracerRejitPreprocessor : public RejitPreprocessor<IntegrationDefinition>
{
public:
    using RejitPreprocessor::RejitPreprocessor;

protected:
    virtual const MethodReference& GetTargetMethod(const IntegrationDefinition& integrationDefinition) final;
    virtual const bool GetIsDerived(const IntegrationDefinition& definition) final;
    virtual const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                 const IntegrationDefinition& integrationDefinition) final;
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_
