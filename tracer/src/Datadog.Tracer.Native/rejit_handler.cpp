#include "rejit_handler.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "logger.h"
#include "stats.h"
#include "function_control_wrapper.h"

namespace trace
{

//
// RejitHandlerModuleMethod
//

RejitHandlerModuleMethod::RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module,
                                                   const FunctionInfo& functionInfo,
                                                   std::unique_ptr<MethodRewriter> methodRewriter) :
    m_methodDef(methodDef),
    m_module(module),
    m_pFunctionControl(nullptr),
    m_functionInfo(std::make_unique<FunctionInfo>(functionInfo)),
    m_methodRewriter(std::move(methodRewriter))
{
}

mdMethodDef RejitHandlerModuleMethod::GetMethodDef()
{
    return m_methodDef;
}

RejitHandlerModule* RejitHandlerModuleMethod::GetModule()
{
    return m_module;
}

FunctionInfo* RejitHandlerModuleMethod::GetFunctionInfo()
{
    return m_functionInfo.get();
}

void RejitHandlerModuleMethod::SetFunctionInfo(const FunctionInfo& functionInfo)
{
    m_functionInfo = std::make_unique<FunctionInfo>(functionInfo);
}

bool RejitHandlerModuleMethod::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    // Enumerate all inliners and request rejit
    ModuleID currentModuleId = m_module->GetModuleId();
    mdMethodDef currentMethodDef = m_methodDef;

    // Let's validate the vars before calling `EnumNgenModuleMethodsInliningThisMethod`
    if (currentModuleId == NULL ||
        moduleId == NULL ||
        currentMethodDef == NULL ||
        currentMethodDef == mdMethodDefNil)
    {
        // we just return true to avoid the retry by the handler.
        Logger::Warn("NGEN:: EnumNgenModuleMethodsInliningThisMethod call skipped by invalid data.");
        return true;
    }

#if DEBUG
    // We generate this log hundreds of times, and isn't typically useful in escalations
    Logger::Debug("RejitHandlerModuleMethod::RequestRejitForInlinersInModule for ", "[ModuleInliner=", moduleId,
                  ", ModuleId=", currentModuleId, ", MethodDef=", currentMethodDef, "]");
#endif

    RejitHandler* handler = m_module->GetHandler();
    ICorProfilerInfo7* pInfo = handler->GetCorProfilerInfo();
    if (pInfo != nullptr)
    {
        // Now we enumerate all methods that inline the current methodDef
        BOOL incompleteData = false;
        ComPtr<ICorProfilerMethodEnum> methodEnum;

        HRESULT hr = pInfo->EnumNgenModuleMethodsInliningThisMethod(moduleId, currentModuleId, currentMethodDef,
                                                                    &incompleteData, methodEnum.GetAddressOf());
        std::ostringstream hexValue;
        hexValue << std::hex << hr;
        if (SUCCEEDED(hr))
        {
            COR_PRF_METHOD method;
            unsigned int total = 0;
            std::vector<MethodIdentifier> methods;
            while (methodEnum->Next(1, &method, nullptr) == S_OK)
            {
                DBG("NGEN:: Asking rewrite for inliner [ModuleId=", method.moduleId, ",MethodDef=", method.methodId, "]");
                methods.emplace_back(method.moduleId, method.methodId);
                total++;
            }

            if (total > 0)
            {
                handler->AddNGenInliners(methods);
                auto requests = handler->GetRejitRequests(methods);
                handler->EnqueueForRejit(std::move(requests));
                Logger::Debug("NGEN:: Processed with ", total, " inliners [ModuleId=", currentModuleId,
                              ",MethodDef=", currentMethodDef, "]");
            }

            if (incompleteData)
            {
                Logger::Warn("NGen inliner data for module '", moduleId, "' is incomplete.");
                return false;
            }
        }
        else if (hr == E_INVALIDARG)
        {
            Logger::Info("NGEN:: Error Invalid arguments in [ModuleId=", currentModuleId,
                         ",MethodDef=", currentMethodDef, ", HR=", hexValue.str(), "]");
        }
        else if (hr == CORPROF_E_DATAINCOMPLETE)
        {
            Logger::Info("NGEN:: Error Incomplete data in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");

            return false;
        }
        else if (hr == CORPROF_E_UNSUPPORTED_CALL_SEQUENCE)
        {
            Logger::Info("NGEN:: Unsupported call sequence error in [ModuleId=", currentModuleId,
                         ",MethodDef=", currentMethodDef, ", HR=", hexValue.str(), "]");
        }
        else
        {
            Logger::Info("NGEN:: Error in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");
        }

        return true;
    }

    return false;
}

MethodRewriter* RejitHandlerModuleMethod::GetMethodRewriter()
{
    return m_methodRewriter.get();
}


//
// RejitHandlerModule
//

RejitHandlerModule::RejitHandlerModule(ModuleID moduleId, RejitHandler* handler) :
    m_moduleId(moduleId), m_handler(handler), m_metadata(nullptr)
{
}

ModuleID RejitHandlerModule::GetModuleId()
{
    return m_moduleId;
}

RejitHandler* RejitHandlerModule::GetHandler()
{
    return m_handler;
}

ModuleMetadata* RejitHandlerModule::GetModuleMetadata()
{
    std::lock_guard<std::mutex> guard(m_metadata_lock);
    return m_metadata.get();
}

// A module lifetime is a shared lease, so several preprocessors can reach the same module at once. Creating
// the metadata has to be a single atomic create-if-absent: a plain set would let the loser of the race delete
// the object that a concurrent rewrite is already working through. Once published the pointer is never
// replaced, so it stays valid for as long as the caller holds the lease.
bool RejitHandlerModule::CreateModuleMetadataIfNotExists(RejitHandlerModuleMetadataCreatorFunc creator)
{
    std::lock_guard<std::mutex> guard(m_metadata_lock);

    if (m_metadata != nullptr)
    {
        return false;
    }

    m_metadata = creator();
    return true;
}

bool RejitHandlerModule::CreateMethodIfNotExists(const mdMethodDef methodDef,
                                                 RejitHandlerModuleMethodCreatorFunc creator,
                                                 RejitHandlerModuleMethodUpdaterFunc updater)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        updater(find_res->second.get());
        return false; // already exist and was not created
    }

    auto newModuleInfo = creator(methodDef, this);
    updater(newModuleInfo.get());
    m_methods[methodDef] = std::move(newModuleInfo);
    return true;
}

bool RejitHandlerModule::TryGetMethod(mdMethodDef methodDef, RejitHandlerModuleMethod** methodHandler)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        *methodHandler = find_res->second.get();
        return true;
    }

    return false;
}

bool RejitHandlerModule::ContainsMethod(mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);
    return m_methods.find(methodDef) != m_methods.end();
}

void RejitHandlerModule::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    std::lock_guard<std::mutex> moduleGuard(m_ngenProcessedInlinerModulesLock);

    // We check first if we already processed this module to skip it.
    auto find_res = m_ngenProcessedInlinerModules.find(moduleId);
    if (find_res != m_ngenProcessedInlinerModules.end())
    {
        return;
    }

    std::lock_guard<std::mutex> methodsGuard(m_methods_lock);
    bool success = true;
    for (const auto& method : m_methods)
    {
        success = success && method.second.get()->RequestRejitForInlinersInModule(moduleId);
        // If we fail to process a method, we stop the processing and try again in another call.
        if (!success)
        {
            break;
        }
    }

    if (success)
    {
        // We mark module as processed.
        m_ngenProcessedInlinerModules[moduleId] = true;
    }
}

//
// RejitHandler
//

void RejitHandler::RequestRejit(const std::vector<RejitRequest>& rejitRequests, bool callRevertExplicitly)
{
    if (IsShutdownRequested() || rejitRequests.empty())
    {
        return;
    }

    std::vector<ModuleID> modulesVector;
    std::vector<mdMethodDef> modulesMethodDef;
    std::vector<ReadLock> lifetimeLocks;
    std::unordered_map<ModuleLifetime*, bool> lifetimeStates;

    modulesVector.reserve(rejitRequests.size());
    modulesMethodDef.reserve(rejitRequests.size());
    lifetimeLocks.reserve(rejitRequests.size());

    // A ModuleID is invalid after ModuleUnloadStarted returns, and Desktop CLR's RequestReJIT path can
    // dereference it directly. Keep every captured module generation alive through the CLR call. Acquire each
    // generation once so a batch containing several methods from one module never recursively locks its
    // shared_mutex. Teardown holds at most one lifetime write lock at a time (under m_module_cleanup_lock), so
    // the batch's read leases cannot form a cycle among themselves. A read lease can still wait behind a queued
    // writer (SRWLOCK blocks new readers), and that writer waits for every current lease holder. Callers must
    // therefore not hold a lock that a lease holder can wait on, such as Dataflow::_cs, which rejitters take
    // under the NotifyReJITParameters lease.
    for (const auto& request : rejitRequests)
    {
        if (request.lifetime == nullptr)
        {
            continue;
        }

        const auto [lifetimeState, inserted] = lifetimeStates.emplace(request.lifetime.get(), false);
        if (inserted)
        {
            auto lifetimeLock = request.Acquire();
            lifetimeState->second = lifetimeLock.has_value();
            if (lifetimeLock.has_value())
            {
                lifetimeLocks.push_back(std::move(lifetimeLock.value()));
            }
        }

        if (lifetimeState->second)
        {
            modulesVector.push_back(request.moduleId);
            modulesMethodDef.push_back(request.methodToken);
        }
    }

    // Request the ReJIT for all integrations found in the module.
    HRESULT hr;

    if (!modulesVector.empty())
    {
        // *************************************
        // Request ReJIT
        // *************************************

        if (callRevertExplicitly)
        {
            HRESULT* status = nullptr;
            m_profilerInfo->RequestRevert((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0], status);
        }

        if (m_profilerInfo10 != nullptr)
        {
            // RequestReJITWithInliners is currently always failing with `Fatal error. Internal CLR error.
            // (0x80131506)` more research is required, meanwhile we fallback to the normal RequestReJIT and
            // manual track of inliners.

            /*hr = m_profilerInfo10->RequestReJITWithInliners(COR_PRF_REJIT_BLOCK_INLINING, (ULONG)
            modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]); if (FAILED(hr))
            {
                Warn("Error requesting ReJITWithInliners for ", vtModules.size(),
                     " methods, falling back to a normal RequestReJIT");
                hr = m_profilerInfo10->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0],
            &modulesMethodDef[0]);
            }*/

            hr = m_profilerInfo10->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]);
        }
        else
        {
            hr = m_profilerInfo->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]);
        }

        // ModuleID validity is only required through the CLR calls. Do not make unload wait for logging or
        // history bookkeeping.
        lifetimeLocks.clear();

        if (SUCCEEDED(hr))
        {
            Logger::Debug("Request ReJIT done for ", modulesVector.size(), " methods");

            if (enable_rejit_tracking)
            {
                WriteLock wlock(m_rejit_history_lock);
                for (size_t i = 0; i < modulesVector.size(); i++)
                {
                    m_rejit_history.push_back({modulesVector[i], modulesMethodDef[i]});
                }
            }
        }
        else
        {
            Logger::Warn("Error requesting ReJIT for ", modulesVector.size(), " methods");
        }
    }
}

void RejitHandler::EnqueueRequestRejit(std::vector<RejitRequest> rejitRequests,
                                       std::shared_ptr<std::promise<void>> promise, 
                                       bool callRevertExplicitly)
{
    EnqueueForRejit(std::move(rejitRequests), promise, callRevertExplicitly);
}

RejitHandler::RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader) :
    m_profilerInfo(pInfo), m_profilerInfo10(nullptr), m_work_offloader(work_offloader)
{
}

RejitHandler::RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader) :
    m_profilerInfo(pInfo), m_profilerInfo10(pInfo), m_work_offloader(work_offloader)
{
}

bool RejitHandler::Enqueue(std::unique_ptr<RejitWorkItem>&& item)
{
    ReadLock lock(m_shutdown_lock);
    if (m_shutdown)
    {
        return false;
    }

    m_work_offloader->Enqueue(std::move(item));
    return true;
}

void RejitHandler::EnqueueForRejit(std::vector<RejitRequest> rejitRequests,
                                   std::shared_ptr<std::promise<void>> promise, bool callRevertExplicitly)
{
    if (IsShutdownRequested() || rejitRequests.empty())
    {
        if (promise != nullptr)
        {
            promise->set_value();
        }

        return;
    }

    DBG("RejitHandler::EnqueueForRejit");

    std::function<void()> action = [=, requests = std::move(rejitRequests), localPromise = promise,
                                    callRevertExplicitly = callRevertExplicitly]() mutable {
        // Request ReJIT
        RequestRejit(requests, callRevertExplicitly);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value();
        }
    };

    // Enqueue
    if (!Enqueue(std::make_unique<RejitWorkItem>(std::move(action))) && promise != nullptr)
    {
        promise->set_value();
    }
}

void RejitHandler::Shutdown()
{
    DBG("RejitHandler::Shutdown");

    // Mark shutdown before draining the queue so queued work can short-circuit. The exchange also makes this
    // idempotent, so only one caller ever enqueues the terminator and joins the worker.
    // Release the write lock before joining because the worker reads this state.
    {
        WriteLock w_lock(m_shutdown_lock);
        if (m_shutdown.exchange(true))
        {
            return;
        }

        m_work_offloader->Enqueue(RejitWorkItem::CreateTerminatingWorkItem());
    }

    // Wait for exiting the thread
    m_work_offloader->WaitForTermination();

    std::lock_guard<std::mutex> cleanupLock(m_module_cleanup_lock);

    std::vector<std::shared_ptr<ModuleLifetime>> moduleLifetimes;
    {
        WriteLock lock(m_module_lifetimes_lock);
        moduleLifetimes.reserve(m_module_lifetimes.size());
        for (const auto& moduleLifetime : m_module_lifetimes)
        {
            moduleLifetimes.push_back(moduleLifetime.second);
        }

        m_module_lifetimes.clear();
    }

    for (const auto& moduleLifetime : moduleLifetimes)
    {
        WriteLock lifetimeLock(moduleLifetime->m_lock);
        moduleLifetime->m_unloading = true;
    }

    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        m_rejitters[x]->Shutdown();
    }

    // m_profilerInfo / m_profilerInfo10 are deliberately left alone. They are owned by the CLR and stay valid
    // for the life of the profiler, so clearing them bought nothing: callers that reach RequestRejit without a
    // module lifetime (iast::Dataflow, JITInlining, the NGEN inliner enumeration) raced this and would null
    // deref instead of simply receiving a failure HRESULT from the runtime.
}

bool RejitHandler::IsShutdownRequested()
{
    // m_shutdown is atomic, and the value can go stale the moment a lock would be released anyway. The
    // shutdown lock is only needed where it orders an enqueue against the terminator, not for advisory reads
    // like this one, which sit on the JIT callback path.
    return m_shutdown;
}

void RejitHandler::RegisterRejitter(Rejitter* rejitter)
{
    if (m_rejittersCount == 0)
    {
        m_rejitters[m_rejittersCount++] = rejitter;
        Logger::Info("RejitHandler::RegisterRejitter -> Registered Rejitter. Count : ", m_rejittersCount);
    }
    else
    {
        size_t x = 0;
        for (; x < m_rejittersCount; x++)
        {
            if (m_rejitters[x]->GetPriority() > rejitter->GetPriority())
            {
                break;
            }
        }

        shared::Insert(m_rejitters, m_rejittersCount, x, rejitter);
        Logger::Info("RejitHandler::RegisterRejitter -> Registered Rejitter at ", x, ". Count : ", m_rejittersCount);
    }
}

HRESULT RejitHandler::NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl)
{
    if (IsShutdownRequested())
    {
        return S_FALSE;
    }

    // Hold the module's lifetime for the whole rewrite: the rejitters read this module's metadata and
    // per-module state, both of which a concurrent unload or Shutdown would tear down underneath us.
    // Nothing below this point may wait on the ReJIT worker, because the worker can be blocked behind an
    // unload that is waiting for this very lifetime (APMS-20456).
    auto module = GetModuleWithLifetime(moduleId);
    auto moduleLifetime = module.Acquire();
    if (!moduleLifetime.has_value())
    {
        return S_FALSE;
    }

    HRESULT hr = S_OK;
    LPCBYTE originalMehodBody = nullptr;
    ULONG originalMehodLen = 0;

    // Create the FunctionControlWrapper
    FunctionControlWrapper functionControl((ICorProfilerInfo*)m_profilerInfo, moduleId, methodId);

    // Call all rejitters sequentially
    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev)
        {
            current->RejitMethod(functionControl);
        }
    }

    return functionControl.ApplyChanges(pFunctionControl);
}

ICorProfilerInfo7* RejitHandler::GetCorProfilerInfo()
{
    return m_profilerInfo;
}

void RejitHandler::SetCorAssemblyProfiler(AssemblyProperty* pCorAssemblyProfiler)
{
    m_pCorAssemblyProperty = pCorAssemblyProfiler;
}

AssemblyProperty* RejitHandler::GetCorAssemblyProperty()
{
    return m_pCorAssemblyProperty;
}

ModuleIDWithLifetime RejitHandler::RegisterModule(ModuleID moduleId)
{
    ReadLock shutdownLock(m_shutdown_lock);
    if (m_shutdown)
    {
        return {moduleId, nullptr};
    }

    WriteLock lock(m_module_lifetimes_lock);
    auto module = m_module_lifetimes.find(moduleId);
    if (module == m_module_lifetimes.end())
    {
        module = m_module_lifetimes.emplace(moduleId, std::make_shared<ModuleLifetime>()).first;
    }

    return {moduleId, module->second};
}

ModuleIDWithLifetime RejitHandler::GetModuleWithLifetime(ModuleID moduleId)
{
    ReadLock lock(m_module_lifetimes_lock);
    const auto module = m_module_lifetimes.find(moduleId);
    return module == m_module_lifetimes.end() ? ModuleIDWithLifetime{moduleId, nullptr}
                                              : ModuleIDWithLifetime{moduleId, module->second};
}

std::vector<ModuleIDWithLifetime> RejitHandler::GetModulesWithLifetime(const std::vector<ModuleID>& moduleIds)
{
    std::vector<ModuleIDWithLifetime> modules;
    modules.reserve(moduleIds.size());

    ReadLock lock(m_module_lifetimes_lock);
    for (const auto moduleId : moduleIds)
    {
        const auto module = m_module_lifetimes.find(moduleId);
        if (module != m_module_lifetimes.end())
        {
            modules.push_back({moduleId, module->second});
        }
    }

    return modules;
}

std::vector<RejitRequest> RejitHandler::GetRejitRequests(const std::vector<MethodIdentifier>& methods)
{
    std::vector<RejitRequest> requests;
    requests.reserve(methods.size());

    ReadLock lock(m_module_lifetimes_lock);
    for (const auto& method : methods)
    {
        const auto module = m_module_lifetimes.find(method.moduleId);
        if (module != m_module_lifetimes.end())
        {
            requests.emplace_back(ModuleIDWithLifetime{method.moduleId, module->second}, method.methodToken);
        }
    }

    return requests;
}

void RejitHandler::SetEnableByRefInstrumentation(bool enableByRefInstrumentation)
{
    enable_by_ref_instrumentation = enableByRefInstrumentation;
}

void RejitHandler::SetEnableCallTargetStateByRef(bool enableCallTargetStateByRef)
{
    enable_calltarget_state_by_ref = enableCallTargetStateByRef;
}

bool RejitHandler::GetEnableCallTargetStateByRef()
{
    return enable_calltarget_state_by_ref;
}

bool RejitHandler::GetEnableByRefInstrumentation()
{
    return enable_by_ref_instrumentation;
}

bool RejitHandler::HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef)
{
    if (IsShutdownRequested())
    {
        return false;
    }

    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev && current->HasModuleAndMethod(moduleId, methodDef))
        {
            return true;
        }
    }

    return false;
}

void RejitHandler::RemoveModule(ModuleID moduleId)
{
    // This cleanup must run even after shutdown is published. Shutdown joins the worker before invalidating
    // all remaining lifetimes, but ModuleUnloadStarted can return during that join and invalidate the CLR's
    // ModuleID first. Removing and marking this generation here makes the worker either finish before unload
    // returns or reject the request.
    // Serialized against Shutdown, which marks every remaining lifetime as unloading.
    std::lock_guard<std::mutex> cleanupLock(m_module_cleanup_lock);

    std::shared_ptr<ModuleLifetime> moduleLifetime;
    {
        WriteLock lock(m_module_lifetimes_lock);
        const auto module = m_module_lifetimes.find(moduleId);
        if (module != m_module_lifetimes.end())
        {
            moduleLifetime = module->second;
            m_module_lifetimes.erase(module);
        }
    }

    std::optional<WriteLock> lifetimeLock;
    if (moduleLifetime != nullptr)
    {
        lifetimeLock.emplace(moduleLifetime->m_lock);
        moduleLifetime->m_unloading = true;
    }

    // Also required after shutdown is published: an NGen inliner replay that passed its shutdown check can still
    // be passing this ModuleID to the CLR under the rejitter's module locks. Only the rejitter's RemoveModule
    // blocks on those locks, which keeps ModuleUnloadStarted from returning until the replay is done.
    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev)
        {
            current->RemoveModule(moduleId);
        }
    }

    // After the rejitters' RemoveModule, so an in-flight NGen inliner replay can't record this module again.
    WriteLock inlinersLock(m_ngen_inliners_lock);
    for (auto it = m_ngen_inliners.begin(); it != m_ngen_inliners.end();)
    {
        it = it->moduleId == moduleId ? m_ngen_inliners.erase(it) : std::next(it);
    }
}

void RejitHandler::AddNGenInlinerModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev)
        {
            current->AddNGenInlinerModule(moduleId);
        }
    }
}

void RejitHandler::AddNGenInliners(const std::vector<MethodIdentifier>& methods)
{
    WriteLock lock(m_ngen_inliners_lock);
    m_ngen_inliners.insert(methods.begin(), methods.end());
}

bool RejitHandler::IsNGenInliner(ModuleID moduleId, mdMethodDef methodDef)
{
    ReadLock lock(m_ngen_inliners_lock);
    return m_ngen_inliners.find(MethodIdentifier(moduleId, methodDef)) != m_ngen_inliners.end();
}

void RejitHandler::SetRejitTracking(bool enabled) {
    if (IsShutdownRequested())
    {
        return;
    }

    enable_rejit_tracking = enabled;
}

bool RejitHandler::HasBeenRejitted(ModuleID moduleId, mdMethodDef methodDef) {
    if (IsShutdownRequested())
    {
        return false;
    }

    if (!enable_rejit_tracking)
    {
        return false;
    }

    ReadLock rlock(m_rejit_history_lock);
    for (size_t i = 0; i < m_rejit_history.size(); i++)
    {
        const auto mod_met_pair = m_rejit_history[i];
        if (get<0>(mod_met_pair) == moduleId && get<1>(mod_met_pair) == methodDef)
        {
            return true;
        }
    }

    return false;
}

} // namespace trace
