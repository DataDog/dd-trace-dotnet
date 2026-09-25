#pragma once
#include <atomic>
#include <future>
#include <memory>
#include <mutex>
#include <optional>
#include <set>
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

class ModuleLifetime
{
    friend class RejitHandler;

private:
    mutable Lock m_lock;
    bool m_unloading = false;

public:
    std::optional<ReadLock> Acquire() const
    {
        ReadLock lock(m_lock);
        if (m_unloading)
        {
            return std::nullopt;
        }

        return std::optional<ReadLock>{std::move(lock)};
    }
};

struct ModuleIDWithLifetime
{
    ModuleID id;
    std::shared_ptr<ModuleLifetime> lifetime;

    std::optional<ReadLock> Acquire() const
    {
        return lifetime == nullptr ? std::nullopt : lifetime->Acquire();
    }
};

struct RejitRequest
{
    ModuleID moduleId;
    mdMethodDef methodToken;
    std::shared_ptr<ModuleLifetime> lifetime;

    RejitRequest(const ModuleIDWithLifetime& module, mdMethodDef methodToken) :
        moduleId(module.id), methodToken(methodToken), lifetime(module.lifetime)
    {
    }

    std::optional<ReadLock> Acquire() const
    {
        return lifetime == nullptr ? std::nullopt : lifetime->Acquire();
    }

    bool operator<(const RejitRequest& rhs) const
    {
        if (moduleId != rhs.moduleId)
        {
            return moduleId < rhs.moduleId;
        }

        if (methodToken != rhs.methodToken)
        {
            return methodToken < rhs.methodToken;
        }

        return lifetime.owner_before(rhs.lifetime);
    }
};

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
using RejitHandlerModuleMetadataCreatorFunc = std::function<std::unique_ptr<ModuleMetadata>()>;

/// <summary>
/// Rejit handler representation of a module
/// </summary>
class RejitHandlerModule
{
private:
    ModuleID m_moduleId;
    std::mutex m_metadata_lock;
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
    bool CreateModuleMetadataIfNotExists(RejitHandlerModuleMetadataCreatorFunc creator);

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

    std::mutex m_module_cleanup_lock;
    Lock m_module_lifetimes_lock;
    std::unordered_map<ModuleID, std::shared_ptr<ModuleLifetime>> m_module_lifetimes;

    Lock m_ngen_inliners_lock;
    std::set<MethodIdentifier> m_ngen_inliners;

    bool enable_by_ref_instrumentation = false;
    bool enable_calltarget_state_by_ref = false;

    #define MAX_REJITTERS 4 // Increase this number when a new rejitter is added
    Rejitter* m_rejitters[MAX_REJITTERS];
    size_t m_rejittersCount = 0;

    Lock m_rejit_history_lock;
    std::vector<std::tuple<ModuleID, mdMethodDef>> m_rejit_history;
    bool enable_rejit_tracking = false;
public:
    RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);
    RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader);

    void RegisterRejitter(Rejitter* rejitter);

    void SetEnableByRefInstrumentation(bool enableByRefInstrumentation);
    void SetEnableCallTargetStateByRef(bool enableCallTargetStateByRef);
    bool GetEnableCallTargetStateByRef();
    bool GetEnableByRefInstrumentation();

    void RequestRejit(const std::vector<RejitRequest>& rejitRequests, bool callRevertExplicitly = false);
    bool Enqueue(std::unique_ptr<RejitWorkItem>&& item);
    void EnqueueForRejit(std::vector<RejitRequest> rejitRequests,
                         std::shared_ptr<std::promise<void>> promise = nullptr,
                         bool callRevertExplicitly = false);
    void EnqueueRequestRejit(std::vector<RejitRequest> rejitRequests, std::shared_ptr<std::promise<void>> promise,
                             bool callRevertExplicitly = false);

    void Shutdown();
    bool IsShutdownRequested();

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl);
 
    ICorProfilerInfo7* GetCorProfilerInfo();

    void SetCorAssemblyProfiler(AssemblyProperty* pCorAssemblyProfiler);
    AssemblyProperty* GetCorAssemblyProperty();

    ModuleIDWithLifetime RegisterModule(ModuleID moduleId);
    ModuleIDWithLifetime GetModuleWithLifetime(ModuleID moduleId);
    std::vector<ModuleIDWithLifetime> GetModulesWithLifetime(const std::vector<ModuleID>& moduleIds);
    std::vector<RejitRequest> GetRejitRequests(const std::vector<MethodIdentifier>& methods);

    bool HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef);
    void RemoveModule(ModuleID moduleId);
    void AddNGenInlinerModule(ModuleID moduleId);
    void AddNGenInliners(const std::vector<MethodIdentifier>& methods);
    bool IsNGenInliner(ModuleID moduleId, mdMethodDef methodDef);

    void SetRejitTracking(bool enabled);
    bool HasBeenRejitted(ModuleID moduleId, mdMethodDef methodDef);
};

} // namespace trace