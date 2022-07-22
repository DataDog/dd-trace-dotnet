#ifndef DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_
#define DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_

#include "rejit_preprocessor.h"
#include "debugger_members.h"

using namespace trace;

namespace debugger
{
/// <summary>
/// DebuggerRejitPreprocessor
/// </summary>
class DebuggerRejitPreprocessor : public RejitPreprocessor<MethodProbeDefinition>
{
public:  
    using RejitPreprocessor::RejitPreprocessor;

    ULONG PreprocessLineProbes(const std::vector<ModuleID>& modules,
                                  const LineProbeDefinitions& lineProbes,
                               std::vector<MethodIdentifier>& rejitRequests) const;
    void EnqueuePreprocessLineProbes(const std::vector<ModuleID>& modules,
                               const LineProbeDefinitions& lineProbes,
                               std::promise<std::vector<MethodIdentifier>>* promise) const;

protected:
    virtual void ProcessTypesForRejit(std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo,
                                      ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit,
                                      ComPtr<IMetaDataAssemblyImport> assemblyImport,
                                      ComPtr<IMetaDataAssemblyEmit> assemblyEmit,
                                      const MethodProbeDefinition& definition,
                                      const MethodReference& targetMethod) final;
    virtual const MethodReference& GetTargetMethod(const MethodProbeDefinition& methodProbe) final;
    virtual const bool GetIsDerived(const MethodProbeDefinition& definition) final;
    virtual const bool GetIsExactSignatureMatch(const MethodProbeDefinition& definition) final;
    virtual const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                 const MethodProbeDefinition& methodProbe) final;
    const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(const mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo) const;
    void UpdateMethod(RejitHandlerModuleMethod* methodHandler, const MethodProbeDefinition& methodProbe) override;
    static void UpdateMethod(RejitHandlerModuleMethod* methodHandler, const ProbeDefinition_S& probe);
    bool ShouldSkipModule(const ModuleInfo& moduleInfo, const MethodProbeDefinition& methodProbe) final;
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_