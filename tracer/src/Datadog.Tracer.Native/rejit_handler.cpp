#include "rejit_handler.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "logger.h"
#include "stats.h"
#include "function_control_wrapper.h"

#include <algorithm>

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

bool RejitHandlerModuleMethod::GetRejitRequestsForInlinersInModule(ModuleID moduleId,
                                                                   std::vector<RejitRequest>& rejitRequests)
{
    // Enumerate all inliners
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
    Logger::Debug("RejitHandlerModuleMethod::GetRejitRequestsForInlinersInModule for ", "[ModuleInliner=", moduleId,
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
                const auto requests = handler->GetRejitRequests(methods);
                rejitRequests.insert(rejitRequests.end(), requests.begin(), requests.end());
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
    m_newMethods.push_back(newModuleInfo.get());
    m_handler->m_hasNewMethods = true;
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

std::vector<RejitHandlerModuleMethod*> RejitHandlerModule::TakeNewMethods()
{
    std::vector<RejitHandlerModuleMethod*> newMethods;
    std::lock_guard<std::mutex> guard(m_methods_lock);
    newMethods.swap(m_newMethods);
    return newMethods;
}

void RejitHandlerModule::GetRejitRequestsForInlinersInModules(const std::vector<ModuleID>& inlinerModules,
                                                              const std::vector<RejitHandlerModuleMethod*>& newMethods,
                                                              std::vector<RejitRequest>& rejitRequests)
{
    std::lock_guard<std::mutex> moduleGuard(m_ngenProcessedInlinerModulesLock);
    for (const auto inlinerModule : inlinerModules)
    {
        // A module already checked against this NGen module only has to check the methods created since then.
        if (m_ngenProcessedInlinerModules.find(inlinerModule) != m_ngenProcessedInlinerModules.end())
        {
            for (const auto method : newMethods)
            {
                if (!method->GetRejitRequestsForInlinersInModule(inlinerModule, rejitRequests))
                {
                    // Check every method again in another call.
                    m_ngenProcessedInlinerModules.erase(inlinerModule);
                    break;
                }
            }

            continue;
        }

        std::lock_guard<std::mutex> methodsGuard(m_methods_lock);
        bool success = true;
        for (const auto& method : m_methods)
        {
            success = method.second->GetRejitRequestsForInlinersInModule(inlinerModule, rejitRequests);
            // If we fail to process a method, we stop the processing and try again in another call.
            if (!success)
            {
                break;
            }
        }

        if (success)
        {
            // We mark module as processed.
            m_ngenProcessedInlinerModules[inlinerModule] = true;
        }
    }
}

void RejitHandlerModule::RemoveProcessedInlinerModule(ModuleID moduleId)
{
    std::lock_guard<std::mutex> moduleGuard(m_ngenProcessedInlinerModulesLock);
    m_ngenProcessedInlinerModules.erase(moduleId);
}

//
// NGenInlinerTracker
//

namespace
{
bool IsUnloaded(const ModuleIDWithLifetime& module)
{
    return module.lifetime == nullptr || module.lifetime->IsUnloading();
}

void SortAndDeduplicate(std::vector<RejitRequest>& rejitRequests)
{
    std::sort(rejitRequests.begin(), rejitRequests.end());
    rejitRequests.erase(std::unique(rejitRequests.begin(), rejitRequests.end(),
                                    [](const RejitRequest& lhs, const RejitRequest& rhs)
                                    { return !(lhs < rhs) && !(rhs < lhs); }),
                        rejitRequests.end());
}
} // namespace

NGenInlinerTracker::NGenInlinerTracker(RejitHandler* handler) : m_handler(handler)
{
}

void NGenInlinerTracker::RecordModule(const ModuleIDWithLifetime& module)
{
    std::lock_guard<std::mutex> inboxGuard(m_inboxLock);
    m_pendingModules.push_back(module);
    m_hasPending = true;
}

void NGenInlinerTracker::RecordMethod(const ModuleIDWithLifetime& module, mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> inboxGuard(m_inboxLock);
    m_pendingMethods.push_back({module, methodDef});
    m_hasPending = true;
}

void NGenInlinerTracker::OnModuleUnloading()
{
    m_unloadEpoch++;
}

std::optional<std::future<void>> NGenInlinerTracker::EnqueueAfterModuleLoad(bool loadContributed)
{
    // A load that recorded no NGen module and instrumented nothing added no pair to check, so it has nothing to wait
    // for. Work pending from elsewhere is left to the next lookup.
    if (!loadContributed)
    {
        return std::nullopt;
    }

    // Blocking is fine: ModuleLoadFinished runs without runtime locks, and the caller holds no module_ids or lease.
    std::lock_guard<std::mutex> lookupGuard(m_lookupLock);
    auto rejitRequests = LookUpInliners();
    if (rejitRequests.empty() && m_inlinerBatchesInFlight == 0)
    {
        // Every caller found so far was requested, including any that another thread found from this load's records.
        return std::nullopt;
    }

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    if (!rejitRequests.empty())
    {
        EnqueueInliners(std::move(rejitRequests), promise);
    }
    else
    {
        // Another thread may have drained this load's records. It counted and enqueued their callers before releasing
        // m_lookupLock (or the .NET Core worker added them to the batch it is running), and the worker runs items in
        // order, so this marker completes after them.
        m_handler->Enqueue(std::make_unique<RejitWorkItem>([promise] { promise->set_value(); },
                                                           [promise] { promise->set_value(); }));
    }

    return future;
}

void NGenInlinerTracker::TryEnqueue()
{
    // The runtime may hold JIT locks during JITCachedFunctionSearchStarted, its hottest NGen path: one atomic load
    // when nothing is pending, and never wait for another lookup. Skipped work stays pending.
    if (!m_hasPending)
    {
        return;
    }

    std::unique_lock<std::mutex> lookupGuard(m_lookupLock, std::try_to_lock);
    if (!lookupGuard.owns_lock())
    {
        return;
    }

    auto rejitRequests = LookUpInliners();
    if (!rejitRequests.empty())
    {
        // Enqueued before releasing m_lookupLock, so an end-of-load marker queues behind these callers.
        EnqueueInliners(std::move(rejitRequests), nullptr);
    }
}

bool NGenInlinerTracker::AppendInliners(std::vector<RejitRequest>& rejitRequests)
{
    if (!m_hasPending)
    {
        return false;
    }

    std::vector<RejitRequest> inliners;
    {
        // Released before the ReJIT: the callers are in the batch the worker is running, ahead of any marker.
        std::lock_guard<std::mutex> lookupGuard(m_lookupLock);
        inliners = LookUpInliners();
        if (inliners.empty())
        {
            return false;
        }

        m_inlinerBatchesInFlight++;
    }

    // One RequestReJIT for the batch and its callers, since each call can suspend the runtime. On .NET Framework the
    // worker can't look up, so EnqueueAfterModuleLoad requests the callers in a batch of their own.
    rejitRequests.insert(rejitRequests.end(), inliners.begin(), inliners.end());
    SortAndDeduplicate(rejitRequests);
    return true;
}

void NGenInlinerTracker::OnAppendedInlinersRequested()
{
    m_inlinerBatchesInFlight--;
}

// Under m_lookupLock, so a load whose lookup finds nothing sees these callers counted.
void NGenInlinerTracker::EnqueueInliners(std::vector<RejitRequest> inliners,
                                         std::shared_ptr<std::promise<void>> promise)
{
    m_inlinerBatchesInFlight++;
    const auto requested = [this, promise]
    {
        m_inlinerBatchesInFlight--;
        if (promise != nullptr)
        {
            promise->set_value();
        }
    };

    m_handler->Enqueue(std::make_unique<RejitWorkItem>(
        [this, requested, inliners = std::move(inliners)]() mutable
        {
            m_handler->RequestRejit(std::move(inliners));
            requested();
        },
        requested));
}

// The only caller of EnumNgenModuleMethodsInliningThisMethod, under m_lookupLock. Allowed in CLR callbacks on both
// runtimes, and on the ReJIT worker on .NET Core. Never on the .NET Framework worker: without a current AppDomain,
// the CLR can crash resolving an inline record instead of returning an error. Managed threads outside a callback get
// CORPROF_E_UNSUPPORTED_CALL_SEQUENCE.
std::vector<RejitRequest> NGenInlinerTracker::LookUpInliners()
{
    if (m_handler->IsShutdownRequested())
    {
        return {};
    }

    m_hasPending = false;
    std::vector<ModuleIDWithLifetime> newModules;
    std::vector<MethodEntry> newMethods;
    {
        std::lock_guard<std::mutex> inboxGuard(m_inboxLock);
        newModules.swap(m_pendingModules);
        newMethods.swap(m_pendingMethods);
    }

    const uint32_t unloadEpoch = m_unloadEpoch;
    if (unloadEpoch != m_seenUnloadEpoch)
    {
        PurgeUnloadedModules();
        m_seenUnloadEpoch = unloadEpoch;
    }

    for (auto& module : newModules)
    {
        if (module.id == 0)
        {
            Logger::Warn("NGEN:: EnumNgenModuleMethodsInliningThisMethod call skipped by invalid data.");
        }
        else if (!IsUnloaded(module))
        {
            m_ngenModules.push_back({std::move(module), 0, {}});
        }
    }

    for (auto& method : newMethods)
    {
        if (method.module.id == 0 || method.methodDef == 0 || method.methodDef == mdMethodDefNil)
        {
            Logger::Warn("NGEN:: EnumNgenModuleMethodsInliningThisMethod call skipped by invalid data.");
        }
        else if (!IsUnloaded(method.module))
        {
            m_methods.push_back(std::move(method));
        }
    }

    // Leases are only tried, never waited for: a lookup holds several, and an unload waiting for one of them must not
    // block it. Each lifetime is tried once per lookup: same-module inlining is common, and shared locks must not be
    // taken recursively. Holding them makes RemoveModule wait until the CLR calls using their ModuleIDs return.
    enum class LeaseState
    {
        Held,
        Contended,
        Unloading
    };
    std::unordered_map<ModuleLifetime*, LeaseState> leaseStates;
    std::vector<ReadLock> leases;
    const auto lease = [&](const ModuleIDWithLifetime& module)
    {
        const auto [leaseState, inserted] = leaseStates.emplace(module.lifetime.get(), LeaseState::Held);
        if (inserted)
        {
            auto lifetimeLock = module.lifetime->TryAcquireNoWait();
            if (lifetimeLock.has_value())
            {
                leases.push_back(std::move(lifetimeLock.value()));
            }
            else
            {
                leaseState->second = module.lifetime->IsUnloading() ? LeaseState::Unloading : LeaseState::Contended;
            }
        }

        return leaseState->second;
    };

    enum class PairResult
    {
        Checked,
        Retry,
        Abort
    };
    ICorProfilerInfo7* pInfo = m_handler->GetCorProfilerInfo();
    std::vector<MethodIdentifier> inliners;
    bool contended = false;
    const auto checkPair = [&](const NGenModuleEntry& ngenModule, const MethodEntry& method)
    {
        const auto inlineeLease = lease(method.module);
        if (inlineeLease == LeaseState::Unloading)
        {
            return PairResult::Checked;
        }

        if (inlineeLease == LeaseState::Contended)
        {
            contended = true;
            return PairResult::Retry;
        }

#if DEBUG
        // We generate this log hundreds of times, and isn't typically useful in escalations
        Logger::Debug("NGenInlinerTracker::LookUpInliners for ", "[ModuleInliner=", ngenModule.module.id,
                      ", ModuleId=", method.module.id, ", MethodDef=", method.methodDef, "]");
#endif

        BOOL incompleteData = false;
        ComPtr<ICorProfilerMethodEnum> methodEnum;
        const HRESULT hr = pInfo->EnumNgenModuleMethodsInliningThisMethod(
            ngenModule.module.id, method.module.id, method.methodDef, &incompleteData, methodEnum.GetAddressOf());
        if (SUCCEEDED(hr))
        {
            // Every caller is requested, even one ReJITed before: its new code may have inlined this method if the
            // method was instrumented after that ReJIT.
            COR_PRF_METHOD inliner;
            unsigned int total = 0;
            while (methodEnum->Next(1, &inliner, nullptr) == S_OK)
            {
                DBG("NGEN:: Asking rewrite for inliner [ModuleId=", inliner.moduleId, ",MethodDef=", inliner.methodId,
                    "]");
                inliners.emplace_back(inliner.moduleId, inliner.methodId);
                total++;
            }

            if (total > 0)
            {
                Logger::Debug("NGEN:: Processed with ", total, " inliners [ModuleId=", method.module.id,
                              ",MethodDef=", method.methodDef, "]");
            }

            // Some callers are in modules that aren't loaded yet. The callers found so far are still requested, and
            // the pair is checked again by the next lookup.
            if (incompleteData)
            {
                Logger::Warn("NGen inliner data for module '", ngenModule.module.id, "' is incomplete.");
                return PairResult::Retry;
            }

            return PairResult::Checked;
        }

        std::ostringstream hexValue;
        hexValue << std::hex << hr;
        if (hr == CORPROF_E_DATAINCOMPLETE)
        {
            // Returned while either module is being unloaded, or when the NGen image has no inline tracking map:
            // neither changes while both modules are loaded.
            Logger::Info("NGEN:: Error Incomplete data in [ModuleId=", method.module.id, ",MethodDef=",
                         method.methodDef, ", HR=", hexValue.str(), "]");
            return PairResult::Checked;
        }

        // A property of the calling thread, not of the pair: stop, and leave everything to a lookup on a supported
        // thread.
        if (hr == CORPROF_E_UNSUPPORTED_CALL_SEQUENCE)
        {
            if (!m_unsupportedThreadLogged)
            {
                m_unsupportedThreadLogged = true;
                Logger::Warn("NGen inliner lookup skipped on an unsupported thread; it will be retried.");
            }

            return PairResult::Abort;
        }

        // Other failures don't change on a later call, so the pair counts as checked.
        if (hr == E_INVALIDARG)
        {
            Logger::Info("NGEN:: Error Invalid arguments in [ModuleId=", method.module.id, ",MethodDef=",
                         method.methodDef, ", HR=", hexValue.str(), "]");
        }
        else
        {
            Logger::Info("NGEN:: Error in [ModuleId=", method.module.id, ",MethodDef=", method.methodDef,
                         ", HR=", hexValue.str(), "]");
        }

        return PairResult::Checked;
    };

    bool aborted = false;
    for (size_t index = 0; index < m_ngenModules.size() && !aborted;)
    {
        auto& ngenModule = m_ngenModules[index];
        if (ngenModule.retry.empty() && ngenModule.checkedUpTo == m_methods.size())
        {
            index++;
            continue;
        }

        const auto ngenModuleLease = lease(ngenModule.module);
        if (ngenModuleLease == LeaseState::Unloading)
        {
            m_ngenModules.erase(m_ngenModules.begin() + index);
            continue;
        }

        if (ngenModuleLease == LeaseState::Contended)
        {
            contended = true;
            index++;
            continue;
        }

        std::vector<MethodEntry> retry;
        retry.swap(ngenModule.retry);
        for (auto& method : retry)
        {
            if (!aborted)
            {
                const auto result = checkPair(ngenModule, method);
                aborted = result == PairResult::Abort;
                if (result == PairResult::Checked)
                {
                    continue;
                }
            }

            ngenModule.retry.push_back(std::move(method));
        }

        for (; !aborted && ngenModule.checkedUpTo < m_methods.size(); ngenModule.checkedUpTo++)
        {
            const auto& method = m_methods[ngenModule.checkedUpTo];
            const auto result = checkPair(ngenModule, method);
            if (result == PairResult::Abort)
            {
                // checkedUpTo stays on this method.
                aborted = true;
                break;
            }

            if (result == PairResult::Retry)
            {
                ngenModule.retry.push_back(method);
            }
        }

        index++;
    }

    // Resolve each caller's own module: in composite ReadyToRun images, a caller can be in another module than the
    // one searched. Done under the leases, so the searched modules' IDs can't be reused meanwhile.
    auto rejitRequests = m_handler->GetRejitRequests(inliners);
    leases.clear();
    SortAndDeduplicate(rejitRequests);

    if (aborted || contended)
    {
        m_hasPending = true;
    }

    return rejitRequests;
}

void NGenInlinerTracker::PurgeUnloadedModules()
{
    // Each checkedUpTo must keep counting the same checked methods: keptBefore[i] is the number of methods kept among
    // the first i. A module can start unloading meanwhile, so its state is read once per method.
    std::vector<size_t> keptBefore(m_methods.size() + 1);
    size_t kept = 0;
    for (size_t index = 0; index < m_methods.size(); index++)
    {
        keptBefore[index] = kept;
        if (!IsUnloaded(m_methods[index].module))
        {
            if (kept != index)
            {
                m_methods[kept] = std::move(m_methods[index]);
            }

            kept++;
        }
    }

    keptBefore[m_methods.size()] = kept;
    m_methods.erase(m_methods.begin() + kept, m_methods.end());

    // A reused ModuleID has a new lifetime, so its new module is recorded and checked from scratch.
    m_ngenModules.erase(std::remove_if(m_ngenModules.begin(), m_ngenModules.end(),
                                       [](const NGenModuleEntry& entry) { return IsUnloaded(entry.module); }),
                        m_ngenModules.end());
    for (auto& ngenModule : m_ngenModules)
    {
        ngenModule.checkedUpTo = keptBefore[ngenModule.checkedUpTo];
        ngenModule.retry.erase(std::remove_if(ngenModule.retry.begin(), ngenModule.retry.end(),
                                              [](const MethodEntry& entry) { return IsUnloaded(entry.module); }),
                               ngenModule.retry.end());
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
            auto lifetimeLock = request.TryAcquire();
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

RejitHandler::RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader,
                           bool isDesktopClr) :
    m_isDesktopClr(isDesktopClr), m_profilerInfo(pInfo), m_profilerInfo10(nullptr), m_work_offloader(work_offloader)
{
}

RejitHandler::RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader,
                           bool isDesktopClr) :
    m_isDesktopClr(isDesktopClr), m_profilerInfo(pInfo), m_profilerInfo10(pInfo), m_work_offloader(work_offloader)
{
}

bool RejitHandler::Enqueue(std::unique_ptr<RejitWorkItem>&& item)
{
    ReadLock lock(m_shutdown_lock);
    if (m_shutdown)
    {
        if (item->abandon != nullptr)
        {
            item->abandon();
        }

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

    std::function<void()> abandon = [localPromise = promise]() {
        if (localPromise != nullptr)
        {
            localPromise->set_value();
        }
    };

    // Enqueue
    Enqueue(std::make_unique<RejitWorkItem>(std::move(action), std::move(abandon)));
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
    auto moduleLifetime = module.TryAcquire();
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
}

void RejitHandler::AddNGenInlinerModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    std::vector<RejitRequest> rejitRequests;
    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev)
        {
            current->AddNGenInlinerModule(moduleId, rejitRequests);
        }
    }

    EnqueueNGenInlinersForRejit(std::move(rejitRequests));
}

std::optional<std::future<void>> RejitHandler::EnqueueRejitForNGenInliners()
{
    if (!m_hasNewMethods.exchange(false))
    {
        return std::nullopt;
    }

    std::vector<RejitRequest> rejitRequests;
    Rejitter* prev = nullptr;
    for (size_t x = 0; x < m_rejittersCount; x++)
    {
        const auto current = m_rejitters[x];
        if (current != prev)
        {
            current->GetNGenInlinerRejitRequests(rejitRequests);
        }
    }

    if (rejitRequests.empty())
    {
        return std::nullopt;
    }

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    EnqueueNGenInlinersForRejit(std::move(rejitRequests), std::move(promise));
    return future;
}

void RejitHandler::EnqueueNGenInlinersForRejit(std::vector<RejitRequest> rejitRequests,
                                               std::shared_ptr<std::promise<void>> promise)
{
    // Every RequestReJIT suspends the runtime on .NET Framework, so request the inliners found by every rejitter
    // at once, and only once each: a method can inline several instrumented methods.
    std::sort(rejitRequests.begin(), rejitRequests.end());
    rejitRequests.erase(std::unique(rejitRequests.begin(), rejitRequests.end(),
                                    [](const RejitRequest& lhs, const RejitRequest& rhs)
                                    { return !(lhs < rhs) && !(rhs < lhs); }),
                        rejitRequests.end());
    EnqueueForRejit(std::move(rejitRequests), std::move(promise));
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
