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
class DebuggerRejitPreprocessor : public RejitPreprocessor<std::shared_ptr<MethodProbeDefinition>>
{
public:
    using RejitPreprocessor::RejitPreprocessor;

    DebuggerRejitPreprocessor(CorProfiler* corProfiler, std::shared_ptr<RejitHandler> rejit_handler,
                            std::shared_ptr<RejitWorkOffloader> work_offloader);

    ULONG PreprocessLineProbes(const std::vector<ModuleID>& modules, const std::vector<std::shared_ptr<LineProbeDefinition>>& lineProbes,
                               std::vector<MethodIdentifier>& rejitRequests);
    void EnqueuePreprocessLineProbes(const std::vector<ModuleID>& modules,
                                     const std::vector<std::shared_ptr<LineProbeDefinition>>& lineProbes,
                               std::promise<std::vector<MethodIdentifier>>* promise);

protected:
    void ProcessTypesForRejit(std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo,
                                      ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit,
                                      ComPtr<IMetaDataAssemblyImport> assemblyImport,
                                      ComPtr<IMetaDataAssemblyEmit> assemblyEmit,
                                      const std::shared_ptr<MethodProbeDefinition>& definition,
                                      const MethodReference& targetMethod) final;
    const MethodReference& GetTargetMethod(const std::shared_ptr<MethodProbeDefinition>& methodProbe) final;
    const bool GetIsDerived(const std::shared_ptr<MethodProbeDefinition>& definition) final;
    const bool GetIsInterface(const std::shared_ptr<MethodProbeDefinition>& definition) final;
    const bool GetIsExactSignatureMatch(const std::shared_ptr<MethodProbeDefinition>& definition) final;
    const bool GetIsEnabled(const std::shared_ptr<MethodProbeDefinition>& definition) final;
    const bool SupportsSelectiveEnablement() final;

    bool CheckExactSignatureMatch(ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo& functionInfo,
                                          const MethodReference& targetMethod) override;

    const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                 const std::shared_ptr<MethodProbeDefinition>& methodProbe) final;
    const std::unique_ptr<RejitHandlerModuleMethod>
    CreateMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo) const;
    void UpdateMethod(RejitHandlerModuleMethod* methodHandler, const std::shared_ptr<MethodProbeDefinition>& methodProbe) override;
    void UpdateMethodInternal(RejitHandlerModuleMethod* methodHandler, const std::shared_ptr<ProbeDefinition>& probe);
    [[nodiscard]] std::tuple<HRESULT, mdMethodDef, FunctionInfo> PickMethodToRejit(
        const ComPtr<IMetaDataImport2>& metadataImport,
        const ComPtr<IMetaDataEmit2>& metadataEmit,
        mdTypeDef typeDef,
        mdMethodDef methodDef,
        const FunctionInfo& functionInfo) const;
    bool ShouldSkipModule(const ModuleInfo& moduleInfo, const std::shared_ptr<MethodProbeDefinition>& methodProbe) final;
    void EnqueueNewMethod(const std::shared_ptr<MethodProbeDefinition>& definition,
                          ComPtr<IMetaDataImport2>& metadataImport,
                          ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo, mdTypeDef typeDef,
                          std::vector<MethodIdentifier>& rejitRequests, unsigned methodDef,
                          const FunctionInfo& functionInfo, RejitHandlerModule* moduleHandler) override;
    static HRESULT GetMoveNextMethodFromKickOffMethod(const ComPtr<IMetaDataImport2>& metadataImport, mdTypeDef typeDef, mdMethodDef methodDef, const FunctionInfo& function,
                                               mdMethodDef& moveNextMethod, mdTypeDef& nestedAsyncClassOrStruct) ;
    static std::tuple<HRESULT, mdMethodDef, FunctionInfo> TransformKickOffToMoveNext(const ComPtr<IMetaDataImport2>& metadataImport,
                                                                                     const ComPtr<IMetaDataEmit2>& metadataEmit,
                                                                                     mdMethodDef moveNextMethod, mdTypeDef nestedAsyncClassOrStruct);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_