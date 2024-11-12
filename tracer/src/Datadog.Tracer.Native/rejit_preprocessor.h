#ifndef DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_
#define DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_

#include "integration.h"
#include <future>
#include "cor.h"
#include "corprof.h"
#include "module_metadata.h"

namespace trace
{
class CorProfiler;
class RejitHandler;
class RejitWorkOffloader;
class RejitHandlerModuleMethod;
class RejitHandlerModule;
class FunctionControlWrapper;
struct FunctionInfo;

enum class RejitterPriority
{
    Critical = 0,
    High = 1,
    Normal = 2,
    Low = 3
};

class Rejitter
{
protected:
    std::shared_ptr<RejitHandler> m_rejitHandler;
    RejitterPriority m_priority;

public:
    Rejitter(std::shared_ptr<RejitHandler> handler, RejitterPriority priority);
    virtual ~Rejitter();

    inline RejitterPriority GetPriority(){ return m_priority; }
    virtual void Shutdown() = 0;
    virtual RejitHandlerModule* GetOrAddModule(ModuleID moduleId) = 0;
    virtual bool HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef) = 0;
    virtual void RemoveModule(ModuleID moduleId) = 0;
    virtual void AddNGenInlinerModule(ModuleID moduleId) = 0;

    virtual HRESULT RejitMethod(FunctionControlWrapper& functionControl) = 0;
};


/// <summary>
/// Responsible to determine what are the methods that should be rejitted and prepares the metadata needed in the rewriting process.
/// </summary>
template <class RejitRequestDefinition>
class RejitPreprocessor : public Rejitter
{
protected:
    CorProfiler* m_corProfiler;
    std::shared_ptr<RejitHandler> m_rejit_handler = nullptr;
    std::shared_ptr<RejitWorkOffloader> m_work_offloader = nullptr;

    void ProcessTypeDefForRejit(const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
                            ComPtr<IMetaDataEmit2>& metadataEmit, ComPtr<IMetaDataAssemblyImport>& assemblyImport,
                            ComPtr<IMetaDataAssemblyEmit>& assemblyEmit, const ModuleInfo& moduleInfo,
                            mdTypeDef typeDef, std::vector<MethodIdentifier>& rejitRequests);

    virtual void ProcessTypesForRejit(std::vector<MethodIdentifier>& rejitRequests, const ModuleInfo& moduleInfo,
                          ComPtr<IMetaDataImport2> metadataImport, ComPtr<IMetaDataEmit2> metadataEmit,
                          ComPtr<IMetaDataAssemblyImport> assemblyImport,
                          ComPtr<IMetaDataAssemblyEmit> assemblyEmit, const RejitRequestDefinition& definition,
                          const MethodReference& targetMethod);

    virtual const MethodReference& GetTargetMethod(const RejitRequestDefinition& definition) = 0;
    virtual const bool GetIsDerived(const RejitRequestDefinition& definition) = 0;
    virtual const bool GetIsInterface(const RejitRequestDefinition& definition) = 0;
    virtual const bool GetIsExactSignatureMatch(const RejitRequestDefinition& definition) = 0;
    virtual const bool GetIsEnabled(const RejitRequestDefinition& definition) = 0;
    virtual const bool SupportsSelectiveEnablement() = 0;

    virtual bool CheckExactSignatureMatch(ComPtr<IMetaDataImport2>& metadataImport, const FunctionInfo& functionInfo,
                                          const MethodReference& targetMethod);

    virtual bool ShouldSkipModule(const ModuleInfo& moduleInfo, const RejitRequestDefinition& definition) = 0;

    virtual const std::unique_ptr<RejitHandlerModuleMethod> CreateMethod(mdMethodDef methodDef,
                                                                         RejitHandlerModule* module,
                                                                         const FunctionInfo& functionInfo,
                                                                         const RejitRequestDefinition& definition) = 0;
    virtual void UpdateMethod(RejitHandlerModuleMethod* method, const RejitRequestDefinition& definition);

    virtual void EnqueueNewMethod(const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
                          ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo, mdTypeDef typeDef,
                          std::vector<MethodIdentifier>& rejitRequests, unsigned methodDef,
                          const FunctionInfo& functionInfo, RejitHandlerModule* moduleHandler);

    ULONG PreprocessRejitRequests(const std::vector<ModuleID>& modules,
                                  const std::vector<RejitRequestDefinition>& definitions,
                                  std::vector<MethodIdentifier>& rejitRequests);

protected:
    std::mutex m_modules_lock;
    std::unordered_map<ModuleID, std::unique_ptr<RejitHandlerModule>> m_modules;
    std::mutex m_ngenInlinersModules_lock;
    std::vector<ModuleID> m_ngenInlinersModules;

public:
    RejitPreprocessor(CorProfiler* corProfiler, std::shared_ptr<RejitHandler> rejit_handler,
                      std::shared_ptr<RejitWorkOffloader> work_offloader, RejitterPriority priority);

    void Shutdown() override;
    RejitHandlerModule* GetOrAddModule(ModuleID moduleId) override;
    bool HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef) override;
    void RemoveModule(ModuleID moduleId) override;
    void AddNGenInlinerModule(ModuleID moduleId) override;
    HRESULT RejitMethod(FunctionControlWrapper& functionControl) override;

    void EnqueueFaultTolerantMethods(const RejitRequestDefinition& definition, ComPtr<IMetaDataImport2>& metadataImport,
                                    ComPtr<IMetaDataEmit2>& metadataEmit, const ModuleInfo& moduleInfo,
                                    mdTypeDef typeDef,
                                    std::vector<MethodIdentifier>& rejitRequests, unsigned methodDef,
                                    const FunctionInfo& functionInfo, RejitHandlerModule* moduleHandler);

    void RequestRejit(std::vector<MethodIdentifier>& rejitRequests, bool enqueueInSameThread = false, bool callRevertExplicitly = false);
    ULONG RequestRejitForLoadedModules(const std::vector<ModuleID>& modules,
                                       const std::vector<RejitRequestDefinition>& requests,
                                       bool enqueueInSameThread = false);
    void EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests, std::shared_ptr<std::promise<void>> promise, bool callRevertExplicitly = false);
    void EnqueueRequestRejitForLoadedModules(const std::vector<ModuleID>& modulesVector,
                                             const std::vector<RejitRequestDefinition>& requests,
                                             std::shared_ptr<std::promise<ULONG>> promise);
    void EnqueuePreprocessRejitRequests(const std::vector<ModuleID>& modules,
                                  const std::vector<RejitRequestDefinition>& definitions,
                                  std::shared_ptr<std::promise<std::vector<MethodIdentifier>>> promise);

};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_PREPROCESSOR_H_
