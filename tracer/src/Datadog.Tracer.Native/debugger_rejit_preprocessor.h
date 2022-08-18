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
    [[nodiscard]] std::tuple<HRESULT, mdMethodDef, FunctionInfo> PickMethodToRejit(
        const ComPtr<IMetaDataImport2>& metadataImport,
        const ComPtr<IMetaDataEmit2>& metadataEmit,
        mdTypeDef typeDef,
        mdMethodDef methodDef,
        const FunctionInfo& functionInfo) const;
    bool ShouldSkipModule(const ModuleInfo& moduleInfo, const MethodProbeDefinition& methodProbe) final;
    void EnqueueNewMethod(const MethodProbeDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
                          ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo, mdTypeDef typeDef,
                          std::vector<MethodIdentifier>& rejitRequests, unsigned methodDef,
                          const FunctionInfo& functionInfo, RejitHandlerModule* moduleHandler) override;
    HRESULT GetMoveNextMethodFromKickOffMethod(const ComPtr<IMetaDataImport2>& metadataImport, mdTypeDef typeDef, mdMethodDef methodDef, const FunctionInfo& function,
                                               mdMethodDef& moveNextMethod, mdTypeDef& nestedAsyncClassOrStruct) const;
    static std::tuple<HRESULT, mdMethodDef, FunctionInfo> TransformKickOffToMoveNext(const ComPtr<IMetaDataImport2>& metadataImport,
                                                                                     const ComPtr<IMetaDataEmit2>& metadataEmit,
                                                                                     mdMethodDef moveNextMethod, mdTypeDef nestedAsyncClassOrStruct);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_REJIT_PREPROCESSOR_H_