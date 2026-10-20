#pragma once
#include <atomic>
#include <future>
#include <memory>
#include <mutex>
#include <optional>
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
    // Atomic so a failed TryAcquireNoWait can tell an unloading module from a contended lease without the lock.
    std::atomic_bool m_unloading = {false};

public:
    [[nodiscard]] std::optional<ReadLock> TryAcquire() const
    {
        ReadLock lock(m_lock);
        if (m_unloading)
        {
            return std::nullopt;
        }

        return std::optional<ReadLock>{std::move(lock)};
    }

    // Never waits, so it can't queue behind an unload that waits for the caller's other leases. Fails if the lock
    // isn't immediately available (an unload holds it or, on Windows, waits for it), or once unloading.
    [[nodiscard]] std::optional<ReadLock> TryAcquireNoWait() const
    {
        ReadLock lock(m_lock, std::try_to_lock);
        if (!lock.owns_lock() || m_unloading)
        {
            return std::nullopt;
        }

        return std::optional<ReadLock>{std::move(lock)};
    }

    [[nodiscard]] bool IsUnloading() const
    {
        return m_unloading;
    }
};

struct ModuleIDWithLifetime
{
    ModuleID id;
    std::shared_ptr<ModuleLifetime> lifetime;

    [[nodiscard]] std::optional<ReadLock> TryAcquire() const
    {
        return lifetime == nullptr ? std::nullopt : lifetime->TryAcquire();
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

    [[nodiscard]] std::optional<ReadLock> TryAcquire() const
    {
        return lifetime == nullptr ? std::nullopt : lifetime->TryAcquire();
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
/// Finds the precompiled (NGen or ReadyToRun) methods that inlined an instrumented method, and requests their ReJIT
/// so they stop running the inlined original.
/// </summary>
class NGenInlinerTracker
{
private:
    struct MethodEntry
    {
        ModuleIDWithLifetime module;
        mdMethodDef methodDef;
    };

    struct NGenModuleEntry
    {
        ModuleIDWithLifetime module;
        // The method at index i of m_methods was checked against this module iff i < checkedUpTo and it isn't in
        // retry.
        size_t checkedUpTo;
        std::vector<MethodEntry> retry;
    };

    RejitHandler* m_handler;

    // Leaf lock: never held while taking another lock or calling the runtime.
    std::mutex m_inboxLock;
    std::vector<ModuleIDWithLifetime> m_pendingModules;
    std::vector<MethodEntry> m_pendingMethods;

    // Set while the inbox isn't empty, or after a lookup that stopped early or met a contended lease. Pairs with
    // incomplete data don't set it: only a module load can complete them, and recording that module runs a lookup.
    std::atomic_bool m_hasPending = {false};
    std::atomic<uint32_t> m_unloadEpoch = {0};

    // Held across the lookup's runtime calls, never across RequestReJIT or a wait. Never acquired (blocking) while
    // holding module_ids, a lease or a rejitter lock: other threads wait on those, and must not wait for a lookup.
    std::mutex m_lookupLock;
    std::vector<MethodEntry> m_methods;
    std::vector<NGenModuleEntry> m_ngenModules;
    uint32_t m_seenUnloadEpoch = 0;
    bool m_unsupportedThreadLogged = false;

    // Batches of callers found by a lookup and not yet requested. Increased under m_lookupLock before it is released,
    // so a load whose lookup finds nothing can tell whether callers found from its records may still be waiting.
    // Decreased once they were requested or abandoned; a missed decrease only adds waits.
    std::atomic<uint32_t> m_inlinerBatchesInFlight = {0};

    std::vector<RejitRequest> LookUpInliners();
    void PurgeUnloadedModules();
    void EnqueueInliners(std::vector<RejitRequest> inliners, std::shared_ptr<std::promise<void>> promise);

public:
    explicit NGenInlinerTracker(RejitHandler* handler);

    // Recording only takes the leaf inbox lock, so it is safe under any other lock.
    void RecordModule(const ModuleIDWithLifetime& module);
    void RecordMethod(const ModuleIDWithLifetime& module, mdMethodDef methodDef);
    // Call after marking the module's lifetime as unloading, so the next lookup purges its entries.
    void OnModuleUnloading();

    // End of ModuleLoadFinished, after waiting for the module's own ReJIT, which creates the methods to check. The
    // future is set once the callers of everything this load recorded or instrumented were requested.
    std::optional<std::future<void>> EnqueueAfterModuleLoad(bool loadContributed);
    // JITCachedFunctionSearchStarted. Never waits.
    void TryEnqueue();
    // .NET Core ReJIT worker only, before RequestRejit takes any lease. Returns whether callers were added; if so, call
    // OnAppendedInlinersRequested after their RequestReJIT.
    bool AppendInliners(std::vector<RejitRequest>& rejitRequests);
    void OnAppendedInlinersRequested();
};

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

    bool GetRejitRequestsForInlinersInModule(ModuleID moduleId, std::vector<RejitRequest>& rejitRequests);
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
    std::vector<RejitHandlerModuleMethod*> m_newMethods;

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
    std::vector<RejitHandlerModuleMethod*> TakeNewMethods();

    // newMethods must belong to this module.
    void GetRejitRequestsForInlinersInModules(const std::vector<ModuleID>& inlinerModules,
                                              const std::vector<RejitHandlerModuleMethod*>& newMethods,
                                              std::vector<RejitRequest>& rejitRequests);
    void RemoveProcessedInlinerModule(ModuleID moduleId);
};

class Rejitter;

/// <summary>
/// Class to control the ReJIT mechanism and to make sure all the required
/// information is present before calling a method rewrite
/// </summary>
class RejitHandler
{
    friend class RejitHandlerModule;

private:
    const bool m_isDesktopClr;
    std::atomic_bool m_shutdown = {false};
    Lock m_shutdown_lock;

    // Set after a method is added to a RejitHandlerModule's new methods, so EnqueueRejitForNGenInliners can skip
    // the NGen lookup when nothing was instrumented since its last call.
    std::atomic_bool m_hasNewMethods = {false};

    AssemblyProperty* m_pCorAssemblyProperty = nullptr;

    ICorProfilerInfo7* m_profilerInfo;
    ICorProfilerInfo10* m_profilerInfo10;

    std::shared_ptr<RejitWorkOffloader> m_work_offloader;

    std::mutex m_module_cleanup_lock;
    Lock m_module_lifetimes_lock;
    std::unordered_map<ModuleID, std::shared_ptr<ModuleLifetime>> m_module_lifetimes;

    bool enable_by_ref_instrumentation = false;
    bool enable_calltarget_state_by_ref = false;

    #define MAX_REJITTERS 4 // Increase this number when a new rejitter is added
    Rejitter* m_rejitters[MAX_REJITTERS];
    size_t m_rejittersCount = 0;

    Lock m_rejit_history_lock;
    std::vector<std::tuple<ModuleID, mdMethodDef>> m_rejit_history;
    bool enable_rejit_tracking = false;

    void EnqueueNGenInlinersForRejit(std::vector<RejitRequest> rejitRequests,
                                     std::shared_ptr<std::promise<void>> promise = nullptr);

public:
    RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader, bool isDesktopClr);
    RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader, bool isDesktopClr);

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
    // Desktop CLR only, and only from a CLR callback: the ReJIT worker has no AppDomain, and the CLR rejects the
    // lookup on other managed threads with CORPROF_E_UNSUPPORTED_CALL_SEQUENCE. Looks up the NGen inliners of the
    // methods instrumented since the last NGen inliner replay, and enqueues one ReJIT for them. The future is set
    // once that ReJIT was requested.
    std::optional<std::future<void>> EnqueueRejitForNGenInliners();

    void SetRejitTracking(bool enabled);
    bool HasBeenRejitted(ModuleID moduleId, mdMethodDef methodDef);
};

} // namespace trace