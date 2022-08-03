#ifndef DD_CLR_PROFILER_REJIT_HANDLER_H_
#define DD_CLR_PROFILER_REJIT_HANDLER_H_

#include <atomic>
#include <mutex>
#include <shared_mutex>
#include <string>
#include <unordered_map>
#include <vector>
#include <future>

#include "cor.h"
#include "corprof.h"
#include "module_metadata.h"
#include "rejit_work_offloader.h"
#include "method_rewriter.h"

namespace trace
{

typedef std::shared_mutex Lock;
typedef std::unique_lock<Lock> WriteLock;
typedef std::shared_lock<Lock> ReadLock;

// forward declarations...
class RejitHandlerModule;
class RejitHandler;

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class RejitHandlerModuleMethod
{
protected:
    mdMethodDef m_methodDef;
    ICorProfilerFunctionControl* m_pFunctionControl;
    std::unique_ptr<FunctionInfo> m_functionInfo;

    RejitHandlerModule* m_module;

public:
    RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo);
    mdMethodDef GetMethodDef();
    RejitHandlerModule* GetModule();

    ICorProfilerFunctionControl* GetFunctionControl();
    void SetFunctionControl(ICorProfilerFunctionControl* pFunctionControl);

    FunctionInfo* GetFunctionInfo();
    void SetFunctionInfo(const FunctionInfo& functionInfo);

    bool RequestRejitForInlinersInModule(ModuleID moduleId);
    virtual MethodRewriter* GetMethodRewriter() = 0;

    virtual ~RejitHandlerModuleMethod() = default;
};

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class TracerRejitHandlerModuleMethod : public RejitHandlerModuleMethod
{
private:
    std::unique_ptr<IntegrationDefinition> m_integrationDefinition;

public:
    TracerRejitHandlerModuleMethod(
                    mdMethodDef methodDef,
                    RejitHandlerModule* module,
                    const FunctionInfo& functionInfo,
                    const IntegrationDefinition& integrationDefinition);
    
    IntegrationDefinition* GetIntegrationDefinition();
    MethodRewriter* GetMethodRewriter() override;
};

using RejitHandlerModuleMethodCreatorFunc = std::function<std::unique_ptr<RejitHandlerModuleMethod>(const mdMethodDef, RejitHandlerModule*)>;
using RejitHandlerModuleMethodUpdaterFunc = std::function<void(RejitHandlerModuleMethod*)>;

/// <summary>
/// Rejit handler representation of a module
/// </summary>
class RejitHandlerModule
{
private:
    ModuleID m_moduleId;
    std::unique_ptr<ModuleMetadata> m_metadata;
    std::mutex m_methods_lock;
    std::unordered_map<mdMethodDef, std::unique_ptr<RejitHandlerModuleMethod>> m_methods;
    
    std::mutex m_ngenProcessedInlinerModulesLock;
    std::unordered_map<ModuleID, bool> m_ngenProcessedInlinerModules;

    RejitHandler* m_handler;

public:
    RejitHandlerModule(ModuleID moduleId, RejitHandler* handler);
    ModuleID GetModuleId();
    RejitHandler* GetHandler();

    ModuleMetadata* GetModuleMetadata();
    void SetModuleMetadata(ModuleMetadata* metadata);

    bool CreateMethodIfNotExists(const mdMethodDef methodDef, RejitHandlerModuleMethodCreatorFunc creator,
                                 RejitHandlerModuleMethodUpdaterFunc updater);
    bool ContainsMethod(mdMethodDef methodDef);
    bool TryGetMethod(mdMethodDef methodDef, /* OUT */ RejitHandlerModuleMethod** methodHandler);

    void RequestRejitForInlinersInModule(ModuleID moduleId);
};

/// <summary>
/// Class to control the ReJIT mechanism and to make sure all the required
/// information is present before calling a method rewrite
/// </summary>
class RejitHandler
{
private:
    std::atomic_bool m_shutdown = {false};
    Lock m_shutdown_lock;

    std::mutex m_modules_lock;
    std::unordered_map<ModuleID, std::unique_ptr<RejitHandlerModule>> m_modules;
    AssemblyProperty* m_pCorAssemblyProperty = nullptr;

    ICorProfilerInfo7* m_profilerInfo;
    ICorProfilerInfo10* m_profilerInfo10;

    std::shared_ptr<RejitWorkOffloader> m_work_offloader;
        
    bool enable_by_ref_instrumentation = false;
    bool enable_calltarget_state_by_ref = false;

    std::mutex m_ngenInlinersModules_lock;
    std::vector<ModuleID> m_ngenInlinersModules;

public:
    RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);
    RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);

    RejitHandlerModule* GetOrAddModule(ModuleID moduleId);
    void SetEnableByRefInstrumentation(bool enableByRefInstrumentation);
    void SetEnableCallTargetStateByRef(bool enableCallTargetStateByRef);
    bool GetEnableCallTargetStateByRef();
    bool GetEnableByRefInstrumentation();

    void RemoveModule(ModuleID moduleId);
    bool HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef);

    void AddNGenInlinerModule(ModuleID moduleId);

    void EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef);
    void RequestRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef);
    void RequestRevert(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef);
    void EnqueueForRevert(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef);

    void Shutdown();
    bool IsShutdownRequested();

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl);
    HRESULT NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId);

    ICorProfilerInfo7* GetCorProfilerInfo();

    void SetCorAssemblyProfiler(AssemblyProperty* pCorAssemblyProfiler);
    AssemblyProperty* GetCorAssemblyProperty();
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_HANDLER_H_