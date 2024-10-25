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
#include "method_rewriter.h"
#include "module_metadata.h"
#include "rejit_work_offloader.h"

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
private:
    std::unique_ptr<MethodRewriter> m_methodRewriter;

protected:
    mdMethodDef m_methodDef;
    ICorProfilerFunctionControl* m_pFunctionControl;
    std::unique_ptr<FunctionInfo> m_functionInfo;

    RejitHandlerModule* m_module;

public:
    RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
                             std::unique_ptr<MethodRewriter> methodRewriter);
    mdMethodDef GetMethodDef();
    RejitHandlerModule* GetModule();

    FunctionInfo* GetFunctionInfo();
    void SetFunctionInfo(const FunctionInfo& functionInfo);

    bool RequestRejitForInlinersInModule(ModuleID moduleId);
    MethodRewriter* GetMethodRewriter();

    virtual ~RejitHandlerModuleMethod() = default;
};

using RejitHandlerModuleMethodCreatorFunc =
    std::function<std::unique_ptr<RejitHandlerModuleMethod>(const mdMethodDef, RejitHandlerModule*)>;
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

    bool CreateMethodIfNotExists(mdMethodDef methodDef, RejitHandlerModuleMethodCreatorFunc creator,
                                 RejitHandlerModuleMethodUpdaterFunc updater);
    bool ContainsMethod(mdMethodDef methodDef);
    bool TryGetMethod(mdMethodDef methodDef, /* OUT */ RejitHandlerModuleMethod** methodHandler);

    void RequestRejitForInlinersInModule(ModuleID moduleId);
};

class Rejitter;

/// <summary>
/// Class to control the ReJIT mechanism and to make sure all the required
/// information is present before calling a method rewrite
/// </summary>
class RejitHandler
{
private:
    std::atomic_bool m_shutdown = {false};
    Lock m_shutdown_lock;

    AssemblyProperty* m_pCorAssemblyProperty = nullptr;

    ICorProfilerInfo7* m_profilerInfo;
    ICorProfilerInfo10* m_profilerInfo10;

    std::shared_ptr<RejitWorkOffloader> m_work_offloader;

    bool enable_by_ref_instrumentation = false;
    bool enable_calltarget_state_by_ref = false;

    std::vector<Rejitter*> m_rejitters;

public:
    RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);
    RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);

    void RegisterRejitter(Rejitter* rejitter);

    void SetEnableByRefInstrumentation(bool enableByRefInstrumentation);
    void SetEnableCallTargetStateByRef(bool enableCallTargetStateByRef);
    bool GetEnableCallTargetStateByRef();
    bool GetEnableByRefInstrumentation();

    void RequestRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef, bool callRevertExplicitly = false);
    void EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef, std::shared_ptr<std::promise<void>> promise = nullptr, bool callRevertExplicitly = false);
    void EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests, std::shared_ptr<std::promise<void>> promise, bool callRevertExplicitly = false);

    void Shutdown();
    bool IsShutdownRequested();

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl);
 
    ICorProfilerInfo7* GetCorProfilerInfo();

    void SetCorAssemblyProfiler(AssemblyProperty* pCorAssemblyProfiler);
    AssemblyProperty* GetCorAssemblyProperty();

    bool HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef);
    void RemoveModule(ModuleID moduleId);
    void AddNGenInlinerModule(ModuleID moduleId);
};

} // namespace trace