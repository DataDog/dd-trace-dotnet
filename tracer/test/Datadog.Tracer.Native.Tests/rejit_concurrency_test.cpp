#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"
#include "../../src/Datadog.Tracer.Native/tracer_rejit_preprocessor.h"
#include "mock_cor_profiler_info.h"

#include <atomic>
#include <chrono>
#include <deque>
#include <future>
#include <map>
#include <thread>
#include <unordered_map>
#include <vector>

using namespace trace;
using namespace std::chrono_literals;

namespace
{
class RejitHandlerContext
{
public:
    MockCorProfilerInfo profilerInfo;
    std::shared_ptr<RejitWorkOffloader> offloader;
    std::unique_ptr<RejitHandler> handler;

    RejitHandlerContext() :
        offloader(std::make_shared<RejitWorkOffloader>(&profilerInfo)),
        handler(std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false))
    {
    }

    ~RejitHandlerContext()
    {
        handler->Shutdown();
    }
};

debugger::ProbeDefinition_S MakeProbe(const shared::WSTRING& probeId)
{
    return std::make_shared<debugger::ProbeDefinition>(shared::WSTRING(probeId));
}

shared::WSTRING ProbeId(int index)
{
    return WStr("probe-") + shared::ToWSTRING(static_cast<uint64_t>(index));
}

template <typename Predicate>
bool WaitUntil(Predicate predicate)
{
    const auto deadline = std::chrono::steady_clock::now() + 1s;
    while (!predicate() && std::chrono::steady_clock::now() < deadline)
    {
        std::this_thread::yield();
    }

    return predicate();
}

class BlockingRejitProfilerInfo : public MockCorProfilerInfo
{
private:
    std::shared_future<void> m_release;

public:
    explicit BlockingRejitProfilerInfo(std::shared_future<void> release) : m_release(std::move(release))
    {
    }

    std::promise<void> requestEntered;
    int revertCalls = 0;
    int rejitCalls = 0;
    std::vector<ModuleID> revertedModules;
    std::vector<mdMethodDef> revertedMethods;
    std::vector<ModuleID> requestedModules;
    std::vector<mdMethodDef> requestedMethods;

    HRESULT STDMETHODCALLTYPE RequestRevert(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[],
                                            HRESULT status[]) override
    {
        revertCalls++;
        revertedModules.assign(moduleIds, moduleIds + cFunctions);
        revertedMethods.assign(methodIds, methodIds + cFunctions);
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override
    {
        rejitCalls++;
        requestedModules.assign(moduleIds, moduleIds + cFunctions);
        requestedMethods.assign(methodIds, methodIds + cFunctions);
        requestEntered.set_value();
        m_release.wait();
        return S_OK;
    }
};

class BlockingNGenProfilerInfo : public MockCorProfilerInfo
{
private:
    std::shared_future<void> m_release;

public:
    explicit BlockingNGenProfilerInfo(std::shared_future<void> release) : m_release(std::move(release))
    {
    }

    std::promise<void> enumerationEntered;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        enumerationEntered.set_value();
        m_release.wait();
        return E_FAIL;
    }
};

class SingleMethodEnum : public ICorProfilerMethodEnum
{
public:
    COR_PRF_METHOD method{};
    bool fetched = false;

    HRESULT STDMETHODCALLTYPE QueryInterface(const IID& riid, void** ppvObject) override { return E_NOINTERFACE; }
    ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
    ULONG STDMETHODCALLTYPE Release() override { return 1; }
    HRESULT STDMETHODCALLTYPE Skip(ULONG celt) override { return E_FAIL; }
    HRESULT STDMETHODCALLTYPE Reset() override { return E_FAIL; }
    HRESULT STDMETHODCALLTYPE Clone(ICorProfilerMethodEnum** ppEnum) override { return E_FAIL; }
    HRESULT STDMETHODCALLTYPE GetCount(ULONG* pcelt) override { return E_FAIL; }

    HRESULT STDMETHODCALLTYPE Next(ULONG celt, COR_PRF_METHOD elements[], ULONG* pceltFetched) override
    {
        if (fetched)
        {
            return S_FALSE;
        }

        fetched = true;
        elements[0] = method;
        return S_OK;
    }
};

class NGenInlinerProfilerInfo : public MockCorProfilerInfo
{
public:
    SingleMethodEnum inliners;
    int enumerations = 0;
    bool firstEnumerationIncomplete = false;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        enumerations++;
        if (firstEnumerationIncomplete && enumerations == 1)
        {
            return CORPROF_E_DATAINCOMPLETE;
        }

        *incompleteData = false;
        *ppEnum = &inliners;
        return S_OK;
    }
};

template <class ProfilerInfo>
class DesktopClrProfilerInfo : public ProfilerInfo
{
public:
    using ProfilerInfo::ProfilerInfo;

    HRESULT STDMETHODCALLTYPE GetRuntimeInformation(USHORT* pClrInstanceId, COR_PRF_RUNTIME_TYPE* pRuntimeType,
                                                    USHORT* pMajorVersion, USHORT* pMinorVersion,
                                                    USHORT* pBuildNumber, USHORT* pQFEVersion, ULONG cchVersionString,
                                                    ULONG* pcchVersionString, WCHAR szVersionString[]) override
    {
        *pRuntimeType = COR_PRF_DESKTOP_CLR;
        *pMajorVersion = 4;
        *pMinorVersion = 0;
        *pBuildNumber = 30319;
        *pQFEVersion = 0;
        return S_OK;
    }
};

using DesktopNGenInlinerProfilerInfo = DesktopClrProfilerInfo<NGenInlinerProfilerInfo>;

// The ReJIT worker writes requestedBatches: read it only after WaitForRejitWorker.
class RecordingNGenProfilerInfo : public MockCorProfilerInfo
{
public:
    std::unordered_map<mdMethodDef, mdMethodDef> inlinerOf;
    mdMethodDef incompleteOnceMethod = mdMethodDefNil;
    std::deque<SingleMethodEnum> inlinerEnums;
    std::vector<mdMethodDef> enumeratedMethods;
    std::vector<std::thread::id> enumerationThreads;
    std::vector<std::vector<mdMethodDef>> requestedBatches;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        enumeratedMethods.push_back(inlineeMethodId);
        enumerationThreads.push_back(std::this_thread::get_id());
        if (inlineeMethodId == incompleteOnceMethod)
        {
            incompleteOnceMethod = mdMethodDefNil;
            return CORPROF_E_DATAINCOMPLETE;
        }

        auto& inliners = inlinerEnums.emplace_back();
        inliners.method = {inlinersModuleId, inlinerOf[inlineeMethodId]};
        *incompleteData = false;
        *ppEnum = &inliners;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override
    {
        requestedBatches.emplace_back(methodIds, methodIds + cFunctions);
        return S_OK;
    }
};

using RecordingDesktopNGenProfilerInfo = DesktopClrProfilerInfo<RecordingNGenProfilerInfo>;

// Blocks the first NGen inliner enumeration until released.
class BlockingRecordingNGenProfilerInfo : public RecordingNGenProfilerInfo
{
private:
    std::shared_future<void> m_release;

public:
    explicit BlockingRecordingNGenProfilerInfo(std::shared_future<void> release) : m_release(std::move(release))
    {
    }

    std::promise<void> enumerationEntered;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        if (enumeratedMethods.empty())
        {
            enumerationEntered.set_value();
            m_release.wait();
        }

        return RecordingNGenProfilerInfo::EnumNgenModuleMethodsInliningThisMethod(
            inlinersModuleId, inlineeModuleId, inlineeMethodId, incompleteData, ppEnum);
    }
};

class CountingNGenProfilerInfo : public MockCorProfilerInfo
{
public:
    std::vector<ModuleID> enumeratedInlinerModules;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        enumeratedInlinerModules.push_back(inlinersModuleId);
        return E_FAIL;
    }
};

using NGenPair = std::pair<ModuleID, mdMethodDef>;

enum class NGenLookupResult
{
    Complete,
    IncompleteData,
    DataIncomplete,
    UnsupportedCallSequence
};

// Inlinee method M is inlined by method M + 10 of every NGen module, unless a test queues another result for that
// (NGen module, inlinee method) pair. The ReJIT worker writes requestedBatches: read it only after WaitForRejitWorker.
class NGenTrackerProfilerInfo : public MockCorProfilerInfo
{
public:
    std::map<NGenPair, std::deque<NGenLookupResult>> queuedResults;
    std::vector<NGenPair> lookups;
    std::deque<SingleMethodEnum> inlinerEnums;
    std::vector<std::vector<NGenPair>> requestedBatches;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        const auto& lookup = lookups.emplace_back(inlinersModuleId, inlineeMethodId);
        auto result = NGenLookupResult::Complete;
        auto queued = queuedResults.find(lookup);
        if (queued != queuedResults.end() && !queued->second.empty())
        {
            result = queued->second.front();
            queued->second.pop_front();
        }

        if (result == NGenLookupResult::DataIncomplete)
        {
            return CORPROF_E_DATAINCOMPLETE;
        }

        if (result == NGenLookupResult::UnsupportedCallSequence)
        {
            return CORPROF_E_UNSUPPORTED_CALL_SEQUENCE;
        }

        auto& inliners = inlinerEnums.emplace_back();
        inliners.method = {inlinersModuleId, inlineeMethodId + 10};
        *incompleteData = result == NGenLookupResult::IncompleteData;
        *ppEnum = &inliners;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override
    {
        auto& batch = requestedBatches.emplace_back();
        for (ULONG i = 0; i < cFunctions; i++)
        {
            batch.emplace_back(moduleIds[i], methodIds[i]);
        }

        return S_OK;
    }
};

// Blocks the first NGen inliner lookup until released.
class BlockingNGenTrackerProfilerInfo : public NGenTrackerProfilerInfo
{
private:
    std::shared_future<void> m_release;

public:
    explicit BlockingNGenTrackerProfilerInfo(std::shared_future<void> release) : m_release(std::move(release))
    {
    }

    std::promise<void> lookupEntered;

    HRESULT STDMETHODCALLTYPE EnumNgenModuleMethodsInliningThisMethod(
        ModuleID inlinersModuleId, ModuleID inlineeModuleId, mdMethodDef inlineeMethodId, BOOL* incompleteData,
        ICorProfilerMethodEnum** ppEnum) override
    {
        if (lookups.empty())
        {
            lookupEntered.set_value();
            m_release.wait();
        }

        return NGenTrackerProfilerInfo::EnumNgenModuleMethodsInliningThisMethod(
            inlinersModuleId, inlineeModuleId, inlineeMethodId, incompleteData, ppEnum);
    }
};

class ObservableTracerRejitPreprocessor : public TracerRejitPreprocessor
{
public:
    using TracerRejitPreprocessor::TracerRejitPreprocessor;
    using TracerRejitPreprocessor::GetNGenInlinerRejitRequestsForNewMethods;

    std::promise<void> removeEntered;
    std::promise<void> removeReturned;

    void RemoveModule(ModuleID moduleId) override
    {
        removeEntered.set_value();
        TracerRejitPreprocessor::RemoveModule(moduleId);
        removeReturned.set_value();
    }
};

void CreateMethod(RejitHandlerModule* module, mdMethodDef methodId)
{
    module->CreateMethodIfNotExists(
        methodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});
}

// The worker runs work items in order, so every request enqueued before this call has reached the CLR when it
// returns. Waiting through Shutdown instead could drop them, because queued requests are skipped after shutdown.
void WaitForRejitWorker(RejitHandler& handler)
{
    auto processed = std::make_shared<std::promise<void>>();
    auto processedFuture = processed->get_future();
    ASSERT_TRUE(handler.Enqueue(std::make_unique<RejitWorkItem>([processed] { processed->set_value(); })));
    processedFuture.wait();
}
} // namespace

TEST(ModuleLifetime, UnloadWaitsForActiveLeaseAndInvalidatesSnapshots)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    const auto module = handler->RegisterModule(moduleId);
    EXPECT_EQ(module.lifetime, handler->RegisterModule(moduleId).lifetime);

    auto moduleLifetime = module.TryAcquire();
    ASSERT_TRUE(moduleLifetime.has_value());

    std::promise<void> unloadFinished;
    auto unloadFinishedFuture = unloadFinished.get_future();
    std::thread unload(
        [&]
        {
            handler->RemoveModule(moduleId);
            unloadFinished.set_value();
        });

    const auto moduleUnpublished =
        WaitUntil([&] { return handler->GetModuleWithLifetime(moduleId).lifetime == nullptr; });
    EXPECT_TRUE(moduleUnpublished);
    EXPECT_EQ(std::future_status::timeout, unloadFinishedFuture.wait_for(0ms));

    moduleLifetime.reset();
    unload.join();

    EXPECT_FALSE(module.TryAcquire().has_value());

    handler->RegisterModule(moduleId);
    const auto reloadedModule = handler->GetModuleWithLifetime(moduleId);
    EXPECT_NE(module.lifetime, reloadedModule.lifetime);
    EXPECT_TRUE(reloadedModule.TryAcquire().has_value());
}

TEST(ModuleLifetime, ShutdownWaitsForActiveLeaseAndInvalidatesModules)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    handler->RegisterModule(moduleId);

    const auto module = handler->GetModuleWithLifetime(moduleId);
    auto moduleLifetime = module.TryAcquire();
    ASSERT_TRUE(moduleLifetime.has_value());

    std::promise<void> shutdownFinished;
    auto shutdownFinishedFuture = shutdownFinished.get_future();
    std::thread shutdown(
        [&]
        {
            handler->Shutdown();
            shutdownFinished.set_value();
        });

    const auto shutdownPublished = WaitUntil(
        [&]
        {
            return handler->IsShutdownRequested() &&
                   handler->GetModuleWithLifetime(moduleId).lifetime == nullptr;
        });
    EXPECT_TRUE(shutdownPublished);
    EXPECT_EQ(std::future_status::timeout, shutdownFinishedFuture.wait_for(0ms));

    moduleLifetime.reset();
    shutdown.join();

    EXPECT_FALSE(module.TryAcquire().has_value());
    EXPECT_FALSE(handler->GetModuleWithLifetime(moduleId).TryAcquire().has_value());

    handler->RegisterModule(moduleId + 1);
    EXPECT_EQ(nullptr, handler->GetModuleWithLifetime(moduleId + 1).lifetime);

    handler->Shutdown();
    EXPECT_TRUE(handler->IsShutdownRequested());
}

TEST(ModuleLifetime, UnloadInvalidatesGenerationWhileShutdownWaitsForWorker)
{
    RejitHandlerContext context;
    const auto& offloader = context.offloader;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    handler->RegisterModule(moduleId);
    const auto module = handler->GetModuleWithLifetime(moduleId);

    std::promise<void> blockerEntered;
    auto blockerEnteredFuture = blockerEntered.get_future();
    std::promise<void> releaseBlocker;
    auto releaseBlockerFuture = releaseBlocker.get_future().share();
    offloader->Enqueue(std::make_unique<RejitWorkItem>(
        [&]
        {
            blockerEntered.set_value();
            releaseBlockerFuture.wait();
        }));
    blockerEnteredFuture.wait();

    std::thread shutdown([&] { handler->Shutdown(); });
    EXPECT_TRUE(WaitUntil([&] { return handler->IsShutdownRequested(); }));

    handler->RemoveModule(moduleId);
    EXPECT_EQ(nullptr, handler->GetModuleWithLifetime(moduleId).lifetime);
    EXPECT_FALSE(module.TryAcquire().has_value());

    releaseBlocker.set_value();
    shutdown.join();
}

TEST(ModuleLifetime, UnloadWaitsForNGenReplayAfterShutdownStarts)
{
    std::promise<void> releaseEnumeration;
    BlockingNGenProfilerInfo profilerInfo(releaseEnumeration.get_future().share());
    auto enumerationEnteredFuture = profilerInfo.enumerationEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    auto removeEnteredFuture = preprocessor.removeEntered.get_future();
    auto removeReturnedFuture = preprocessor.removeReturned.get_future();
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef methodId = 1;
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    module->CreateMethodIfNotExists(
        methodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    std::promise<void> blockerEntered;
    auto blockerEnteredFuture = blockerEntered.get_future();
    std::promise<void> releaseBlocker;
    auto releaseBlockerFuture = releaseBlocker.get_future().share();
    offloader->Enqueue(std::make_unique<RejitWorkItem>(
        [&]
        {
            blockerEntered.set_value();
            releaseBlockerFuture.wait();
        }));
    const auto blockerStarted = blockerEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(blockerStarted);
    if (!blockerStarted)
    {
        releaseBlocker.set_value();
        handler->Shutdown();
        return;
    }

    std::thread replay([&] { handler->AddNGenInlinerModule(inlinersModuleId); });
    const auto enumerationEntered = enumerationEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(enumerationEntered);
    if (!enumerationEntered)
    {
        releaseEnumeration.set_value();
        replay.join();
        releaseBlocker.set_value();
        handler->Shutdown();
        return;
    }

    std::thread shutdown([&] { handler->Shutdown(); });
    const auto shutdownPublished = WaitUntil([&] { return handler->IsShutdownRequested(); });
    EXPECT_TRUE(shutdownPublished);
    if (!shutdownPublished)
    {
        releaseEnumeration.set_value();
        replay.join();
        releaseBlocker.set_value();
        shutdown.join();
        return;
    }

    std::thread unload([&] { handler->RemoveModule(inlinersModuleId); });

    // ModuleUnloadStarted must remain blocked while the CLR can still dereference this generation.
    EXPECT_EQ(std::future_status::ready, removeEnteredFuture.wait_for(1s));
    EXPECT_EQ(std::future_status::timeout, removeReturnedFuture.wait_for(100ms));

    releaseEnumeration.set_value();
    replay.join();
    unload.join();

    releaseBlocker.set_value();
    shutdown.join();
}

TEST(ModuleLifetime, ReusedNGenInlinerModuleIdIsEnumeratedAgain)
{
    CountingNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    TracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef methodId = 1;

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    module->CreateMethodIfNotExists(
        methodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    handler->AddNGenInlinerModule(inlinersModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    EXPECT_EQ(std::vector<ModuleID>{inlinersModuleId}, profilerInfo.enumeratedInlinerModules);

    // Desktop CLR hands the unloaded module's ID to a different NGen image.
    handler->RemoveModule(inlinersModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    EXPECT_EQ((std::vector<ModuleID>{inlinersModuleId, inlinersModuleId}), profilerInfo.enumeratedInlinerModules);

    handler->Shutdown();
}

TEST(RejitHandlerShutdown, EnqueueForRejitResolvesPromiseAfterShutdown)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 1;
    handler->RegisterModule(moduleId);

    std::vector<MethodIdentifier> methods{{moduleId, 2}};
    auto requests = handler->GetRejitRequests(methods);
    handler->Shutdown();

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();

    handler->EnqueueForRejit(std::move(requests), promise);

    EXPECT_EQ(std::future_status::ready, future.wait_for(1s));
}

TEST(RejitHandlerShutdown, RefusedWorkItemReleasesWaiter)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    handler->Shutdown();

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    EXPECT_FALSE(handler->Enqueue(std::make_unique<RejitWorkItem>([] {}, [promise] { promise->set_value(); })));

    EXPECT_EQ(std::future_status::ready, future.wait_for(0s));
}

TEST(RejitHandlerShutdown, AcceptedWorkCompletesBeforeTerminator)
{
    constexpr int iterations = 200;
    constexpr int producersCount = 4;
    int lostItems = 0;
    for (int iteration = 0; iteration < iterations; iteration++)
    {
        std::atomic<int> accepted{0};
        std::atomic<int> completed{0};
        RejitHandlerContext context;
        const auto& handler = context.handler;

        std::vector<std::thread> producers;
        for (int producer = 0; producer < producersCount; producer++)
        {
            producers.emplace_back(
                [&]
                {
                    while (handler->Enqueue(std::make_unique<RejitWorkItem>([&completed] { completed++; })))
                    {
                        accepted++;
                    }
                });
        }

        EXPECT_TRUE(WaitUntil([&] { return accepted > 0; }));
        handler->Shutdown();
        for (auto& producer : producers)
        {
            producer.join();
        }

        // Shutdown joined the worker, so every item accepted ahead of the terminator has already run.
        lostItems += accepted - completed;
    }

    EXPECT_EQ(0, lostItems);
}

TEST(RejitHandlerShutdown, EnqueueForRejitRacingShutdownReleasesEveryWaiter)
{
    constexpr int iterations = 200;
    constexpr int producersCount = 4;
    constexpr ModuleID moduleId = 1;
    const std::vector<MethodIdentifier> methods{{moduleId, 2}};
    int unreleasedWaiters = 0;
    for (int iteration = 0; iteration < iterations; iteration++)
    {
        std::atomic<int> enqueued{0};
        // Each promise stays alive like the tracer's callers keep theirs, so a missed release is a hang rather
        // than a broken_promise.
        std::vector<std::vector<std::pair<std::shared_ptr<std::promise<void>>, std::future<void>>>> waiters(
            producersCount);
        RejitHandlerContext context;
        const auto& handler = context.handler;
        handler->RegisterModule(moduleId);

        std::vector<std::thread> producers;
        for (int producer = 0; producer < producersCount; producer++)
        {
            producers.emplace_back(
                [&, producer]
                {
                    while (!handler->IsShutdownRequested())
                    {
                        auto promise = std::make_shared<std::promise<void>>();
                        waiters[producer].emplace_back(promise, promise->get_future());
                        handler->EnqueueForRejit(handler->GetRejitRequests(methods), promise);
                        enqueued++;
                    }
                });
        }

        EXPECT_TRUE(WaitUntil([&] { return enqueued > 0; }));
        handler->Shutdown();
        for (auto& producer : producers)
        {
            producer.join();
        }

        for (const auto& producerWaiters : waiters)
        {
            for (const auto& waiter : producerWaiters)
            {
                if (waiter.second.wait_for(0s) != std::future_status::ready)
                {
                    unreleasedWaiters++;
                }
            }
        }
    }

    EXPECT_EQ(0, unreleasedWaiters);
}

TEST(RejitHandler, RequestRejitSkipsUnloadedGenerationAfterModuleIdReuse)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    std::vector<MethodIdentifier> methods{{moduleId, 1}};

    handler->RegisterModule(moduleId);
    auto oldRequests = handler->GetRejitRequests(methods);
    ASSERT_EQ(1u, oldRequests.size());

    handler->RemoveModule(moduleId);
    handler->RegisterModule(moduleId);

    const auto newRequests = handler->GetRejitRequests(methods);
    ASSERT_EQ(1u, newRequests.size());
    EXPECT_NE(oldRequests[0].lifetime, newRequests[0].lifetime);
    EXPECT_FALSE(oldRequests[0].TryAcquire().has_value());

    handler->RequestRejit(oldRequests);
    EXPECT_EQ(0, context.profilerInfo.requestRejitCallCount);
}

TEST(RejitPreprocessor, NewMethodGetsNGenInlinersFromAlreadyCheckedModule)
{
    NGenInlinerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef inlineeMethodId = 1;
    constexpr mdMethodDef inlinerMethodId = 2;
    profilerInfo.inliners.method = {inlinersModuleId, inlinerMethodId};
    const auto inlineeModule = handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    // The module is checked against the NGen module while it has no methods yet.
    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    module->CreateMethodIfNotExists(
        inlineeMethodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    auto moduleLifetime = inlineeModule.TryAcquire();
    EXPECT_TRUE(moduleLifetime.has_value());
    std::vector<RejitRequest> requests;
    preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);

    EXPECT_EQ(1u, requests.size());
    for (const auto& request : requests)
    {
        EXPECT_EQ(inlinersModuleId, request.moduleId);
        EXPECT_EQ(inlinerMethodId, request.methodToken);
        EXPECT_TRUE(request.TryAcquire().has_value());
    }

    // A method is checked only once.
    preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);
    EXPECT_EQ(1, profilerInfo.enumerations);

    moduleLifetime.reset();
    handler->Shutdown();
}

TEST(RejitPreprocessor, IncompleteNGenDataForNewMethodIsRetriedByReplay)
{
    NGenInlinerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef inlineeMethodId = 1;
    constexpr mdMethodDef inlinerMethodId = 2;
    profilerInfo.inliners.method = {inlinersModuleId, inlinerMethodId};
    profilerInfo.firstEnumerationIncomplete = true;
    const auto inlineeModule = handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    module->CreateMethodIfNotExists(
        inlineeMethodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    {
        auto moduleLifetime = inlineeModule.TryAcquire();
        std::vector<RejitRequest> requests;
        preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);
        EXPECT_TRUE(requests.empty());
    }

    handler->AddNGenInlinerModule(inlinersModuleId);

    EXPECT_EQ(2, profilerInfo.enumerations);

    handler->Shutdown();
}

TEST(RejitPreprocessor, DesktopNewMethodNGenInlinersAreDeferredToReplay)
{
    DesktopNGenInlinerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef inlineeMethodId = 1;
    constexpr mdMethodDef inlinerMethodId = 2;
    profilerInfo.inliners.method = {inlinersModuleId, inlinerMethodId};
    const auto inlineeModule = handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    // The module is checked against the NGen module while it has no methods yet.
    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    module->CreateMethodIfNotExists(
        inlineeMethodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    {
        auto moduleLifetime = inlineeModule.TryAcquire();
        std::vector<RejitRequest> requests;
        preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);

        // The ReJIT worker has no AppDomain on Desktop CLR, so it must not enumerate NGen inliners.
        EXPECT_TRUE(requests.empty());
        EXPECT_EQ(0, profilerInfo.enumerations);
    }

    // The next replay checks the module again and finds the new method's inliners.
    handler->AddNGenInlinerModule(inlinersModuleId);

    EXPECT_EQ(1, profilerInfo.enumerations);

    handler->Shutdown();
}

TEST(RejitPreprocessor, DesktopReplayChecksOnlyNewMethodsAndRequestsInlinersInOneBatch)
{
    RecordingDesktopNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}, {2, 12}, {3, 12}};
    const auto inlineeModule = handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    CreateMethod(module, 1);
    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ(std::vector<mdMethodDef>{1}, profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}}), profilerInfo.requestedBatches);

    CreateMethod(module, 2);
    CreateMethod(module, 3);
    {
        auto moduleLifetime = inlineeModule.TryAcquire();
        std::vector<RejitRequest> requests;
        preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);
        EXPECT_TRUE(requests.empty());
    }

    // Method 1 was already checked against the NGen module, and inliner 12 inlines both new methods.
    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2, 3}), profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}, {12}}), profilerInfo.requestedBatches);

    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2, 3}), profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}, {12}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(RejitPreprocessor, DesktopIncompleteNGenDataForNewMethodRechecksModule)
{
    RecordingDesktopNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}, {2, 12}};
    profilerInfo.incompleteOnceMethod = 2;
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    CreateMethod(module, 1);
    handler->AddNGenInlinerModule(inlinersModuleId);
    CreateMethod(module, 2);
    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2}), profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}}), profilerInfo.requestedBatches);

    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ(4u, profilerInfo.enumeratedMethods.size());
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}, {11, 12}}), profilerInfo.requestedBatches);

    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ(4u, profilerInfo.enumeratedMethods.size());
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}, {11, 12}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(RejitHandler, NGenReplaySendsOneBatchForAllRejitters)
{
    RecordingNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    TracerRejitPreprocessor firstPreprocessor(nullptr, handler);
    TracerRejitPreprocessor secondPreprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}, {2, 12}, {3, 12}};
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto firstModule = firstPreprocessor.GetOrAddModule(inlineeModuleId);
    CreateMethod(firstModule, 1);
    CreateMethod(firstModule, 2);
    auto secondModule = secondPreprocessor.GetOrAddModule(inlineeModuleId);
    CreateMethod(secondModule, 1);
    CreateMethod(secondModule, 3);

    // Both rejitters find inliner 11, and inliner 12 inlines two instrumented methods.
    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ(4u, profilerInfo.enumeratedMethods.size());
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11, 12}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(RejitHandler, DesktopNGenInlinerLookupRequestsInlinersInOneBatchWithoutWaiting)
{
    RecordingDesktopNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    TracerRejitPreprocessor firstPreprocessor(nullptr, handler);
    TracerRejitPreprocessor secondPreprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}, {2, 12}, {3, 12}};
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    // The NGen module loads, and its callers run, before anything is instrumented.
    auto firstModule = firstPreprocessor.GetOrAddModule(inlineeModuleId);
    auto secondModule = secondPreprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);

    // Both rejitters find inliner 11, and inliner 12 inlines two instrumented methods.
    CreateMethod(firstModule, 1);
    CreateMethod(firstModule, 2);
    CreateMethod(secondModule, 1);
    CreateMethod(secondModule, 3);

    std::promise<void> releaseWorker;
    ASSERT_TRUE(handler->Enqueue(std::make_unique<RejitWorkItem>(
        [releaseWorkerFuture = releaseWorker.get_future().share()] { releaseWorkerFuture.wait(); })));
    auto rejit = handler->EnqueueRejitForNGenInliners();
    const auto requestedBeforeWorkerRan = rejit.has_value() && rejit->wait_for(0s) == std::future_status::ready;
    releaseWorker.set_value();
    WaitForRejitWorker(*handler);

    EXPECT_FALSE(requestedBeforeWorkerRan);
    EXPECT_TRUE(rejit.has_value() && rejit->wait_for(0s) == std::future_status::ready);
    // The ReJIT worker has no AppDomain on Desktop CLR, so only the calling thread may enumerate NGen inliners.
    EXPECT_EQ(std::vector<std::thread::id>(4, std::this_thread::get_id()), profilerInfo.enumerationThreads);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11, 12}}), profilerInfo.requestedBatches);

    EXPECT_FALSE(handler->EnqueueRejitForNGenInliners().has_value());
    EXPECT_EQ(4u, profilerInfo.enumeratedMethods.size());

    handler->Shutdown();
}

TEST(RejitHandler, DesktopNGenInlinerLookupSkipsWithoutNewMethods)
{
    RecordingDesktopNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    TracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}};
    profilerInfo.incompleteOnceMethod = 1;
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    CreateMethod(module, 1);
    EXPECT_FALSE(handler->EnqueueRejitForNGenInliners().has_value());
    EXPECT_EQ(std::vector<mdMethodDef>{1}, profilerInfo.enumeratedMethods);

    // The incomplete data left the module to check again, but nothing was instrumented since, so the retry is left
    // to the next NGen module load or cached function search.
    EXPECT_FALSE(handler->EnqueueRejitForNGenInliners().has_value());
    EXPECT_EQ(std::vector<mdMethodDef>{1}, profilerInfo.enumeratedMethods);

    handler->AddNGenInlinerModule(inlinersModuleId);
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 1}), profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(RejitHandler, DesktopNGenInlinerLookupChecksMethodsCreatedDuringLookupNextTime)
{
    std::promise<void> releaseEnumeration;
    DesktopClrProfilerInfo<BlockingRecordingNGenProfilerInfo> profilerInfo(releaseEnumeration.get_future().share());
    auto enumerationEnteredFuture = profilerInfo.enumerationEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
    TracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}, {2, 12}};
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    CreateMethod(module, 1);

    std::thread lookup([&] { handler->EnqueueRejitForNGenInliners(); });
    const auto enumerationEntered = enumerationEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(enumerationEntered);
    if (enumerationEntered)
    {
        // Instrumented after the lookup took the new methods.
        CreateMethod(module, 2);
    }

    releaseEnumeration.set_value();
    lookup.join();

    handler->EnqueueRejitForNGenInliners();
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2}), profilerInfo.enumeratedMethods);
    EXPECT_EQ((std::vector<std::vector<mdMethodDef>>{{11}, {12}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(RejitHandler, NGenInlinerLookupIsLeftToReJitWorkerOnCoreClr)
{
    RecordingNGenProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    TracerRejitPreprocessor preprocessor(nullptr, handler);
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    profilerInfo.inlinerOf = {{1, 11}};
    handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    CreateMethod(module, 1);

    EXPECT_FALSE(handler->EnqueueRejitForNGenInliners().has_value());
    EXPECT_TRUE(profilerInfo.enumeratedMethods.empty());

    handler->Shutdown();
}

TEST(ModuleLifetime, UnloadWaitsForNewMethodNGenCheck)
{
    std::promise<void> releaseEnumeration;
    BlockingNGenProfilerInfo profilerInfo(releaseEnumeration.get_future().share());
    auto enumerationEnteredFuture = profilerInfo.enumerationEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
    auto removeEnteredFuture = preprocessor.removeEntered.get_future();
    auto removeReturnedFuture = preprocessor.removeReturned.get_future();
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;
    constexpr mdMethodDef methodId = 1;
    const auto inlineeModule = handler->RegisterModule(inlineeModuleId);
    handler->RegisterModule(inlinersModuleId);

    auto module = preprocessor.GetOrAddModule(inlineeModuleId);
    handler->AddNGenInlinerModule(inlinersModuleId);
    module->CreateMethodIfNotExists(
        methodId,
        [](mdMethodDef methodDef, RejitHandlerModule* moduleHandler)
        {
            return std::make_unique<RejitHandlerModuleMethod>(
                methodDef, moduleHandler, FunctionInfo{}, std::unique_ptr<MethodRewriter>{});
        },
        [](RejitHandlerModuleMethod*) {});

    std::thread check(
        [&]
        {
            auto moduleLifetime = inlineeModule.TryAcquire();
            std::vector<RejitRequest> requests;
            preprocessor.GetNGenInlinerRejitRequestsForNewMethods(inlineeModuleId, requests);
        });
    const auto enumerationEntered = enumerationEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(enumerationEntered);
    if (!enumerationEntered)
    {
        releaseEnumeration.set_value();
        check.join();
        handler->Shutdown();
        return;
    }

    std::thread unload([&] { handler->RemoveModule(inlinersModuleId); });

    // ModuleUnloadStarted must remain blocked while the CLR can still dereference the NGen module.
    EXPECT_EQ(std::future_status::ready, removeEnteredFuture.wait_for(1s));
    EXPECT_EQ(std::future_status::timeout, removeReturnedFuture.wait_for(100ms));

    releaseEnumeration.set_value();
    check.join();
    unload.join();

    handler->Shutdown();
}

TEST(ModuleLifetime, UnloadWaitsForNGenInlinerLookup)
{
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID inlinersModuleId = 42;

    // The lookup passes both ModuleIDs to the CLR.
    for (const auto unloadedModuleId : {inlineeModuleId, inlinersModuleId})
    {
        SCOPED_TRACE(unloadedModuleId);
        std::promise<void> releaseEnumeration;
        DesktopClrProfilerInfo<BlockingNGenProfilerInfo> profilerInfo(releaseEnumeration.get_future().share());
        auto enumerationEnteredFuture = profilerInfo.enumerationEntered.get_future();
        auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
        auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, true);
        ObservableTracerRejitPreprocessor preprocessor(nullptr, handler);
        auto removeEnteredFuture = preprocessor.removeEntered.get_future();
        auto removeReturnedFuture = preprocessor.removeReturned.get_future();
        handler->RegisterModule(inlineeModuleId);
        handler->RegisterModule(inlinersModuleId);

        auto module = preprocessor.GetOrAddModule(inlineeModuleId);
        handler->AddNGenInlinerModule(inlinersModuleId);
        CreateMethod(module, 1);

        std::thread lookup([&] { handler->EnqueueRejitForNGenInliners(); });
        const auto enumerationEntered = enumerationEnteredFuture.wait_for(1s) == std::future_status::ready;
        EXPECT_TRUE(enumerationEntered);
        if (!enumerationEntered)
        {
            releaseEnumeration.set_value();
            lookup.join();
            handler->Shutdown();
            continue;
        }

        std::thread unload([&] { handler->RemoveModule(unloadedModuleId); });

        // ModuleUnloadStarted must remain blocked while the CLR can still dereference the module. Nothing signals
        // that RemoveModule is still blocked, so give it time to return if the lookup doesn't hold it back.
        EXPECT_EQ(std::future_status::ready, removeEnteredFuture.wait_for(1s));
        EXPECT_EQ(std::future_status::timeout, removeReturnedFuture.wait_for(100ms));

        releaseEnumeration.set_value();
        lookup.join();
        unload.join();

        handler->Shutdown();
    }
}

TEST(NGenInlinerTracker, ChecksEachMethodAndNGenModulePairOnce)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    const auto inlineeModule = handler->RegisterModule(41);

    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(inlineeModule, 1);
    tracker.RecordMethod(inlineeModule, 2);
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}}), profilerInfo.lookups);

    // A new NGen module is checked against every method.
    tracker.RecordModule(handler->RegisterModule(43));
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}}), profilerInfo.lookups);

    // A new method is checked against every NGen module.
    tracker.RecordMethod(inlineeModule, 3);
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}, {42, 3}, {43, 3}}), profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{{{42, 11}, {42, 12}}, {{43, 11}, {43, 12}}, {{42, 13}, {43, 13}}}),
              profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, IncompleteDataRetriesOnlyThatPairAfterTheNextRecord)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    const auto inlineeModule = handler->RegisterModule(41);
    profilerInfo.queuedResults[{42, 2}] = {NGenLookupResult::IncompleteData};

    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(inlineeModule, 1);
    tracker.RecordMethod(inlineeModule, 2);
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}}), profilerInfo.lookups);

    // Only a module load can complete the data, so the pair alone doesn't keep the lookup pending.
    tracker.TryEnqueue();
    EXPECT_EQ(2u, profilerInfo.lookups.size());

    tracker.RecordModule(handler->RegisterModule(43));
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {42, 2}, {43, 1}, {43, 2}}), profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{{{42, 11}, {42, 12}}, {{42, 12}, {43, 11}, {43, 12}}}),
              profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, DataIncompleteErrorCountsAsChecked)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    const auto inlineeModule = handler->RegisterModule(41);
    profilerInfo.queuedResults[{42, 2}] = {NGenLookupResult::DataIncomplete};

    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(inlineeModule, 1);
    tracker.RecordMethod(inlineeModule, 2);
    tracker.TryEnqueue();
    tracker.RecordModule(handler->RegisterModule(43));
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);

    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}}), profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{{{42, 11}}, {{43, 11}, {43, 12}}}), profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, UnsupportedCallSequenceStopsLookupAndKeepsWorkPending)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    const auto inlineeModule = handler->RegisterModule(41);
    profilerInfo.queuedResults[{42, 2}] = {NGenLookupResult::IncompleteData, NGenLookupResult::UnsupportedCallSequence};
    profilerInfo.queuedResults[{43, 3}] = {NGenLookupResult::UnsupportedCallSequence};

    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordModule(handler->RegisterModule(43));
    tracker.RecordMethod(inlineeModule, 1);
    tracker.RecordMethod(inlineeModule, 2);
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}}), profilerInfo.lookups);

    // Stops while retrying (42, 2), before checking the new method.
    tracker.RecordMethod(inlineeModule, 3);
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}, {42, 2}}), profilerInfo.lookups);

    // Stops on (43, 3), but still requests the callers it found before.
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}, {42, 2}, {42, 2}, {42, 3}, {43, 3}, {43, 3}}),
              profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{
                  {{42, 11}, {42, 12}, {43, 11}, {43, 12}}, {{42, 12}, {42, 13}}, {{43, 13}}}),
              profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, NoLookupForUnloadingModules)
{
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID ngenModuleId = 42;
    for (const auto unloadedModuleId : {inlineeModuleId, ngenModuleId})
    {
        SCOPED_TRACE(unloadedModuleId);
        NGenTrackerProfilerInfo profilerInfo;
        auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
        auto handler =
            std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
        NGenInlinerTracker tracker(handler.get());
        const auto inlineeModule = handler->RegisterModule(inlineeModuleId);

        tracker.RecordModule(handler->RegisterModule(ngenModuleId));
        tracker.RecordMethod(inlineeModule, 1);
        tracker.TryEnqueue();
        EXPECT_EQ((std::vector<NGenPair>{{42, 1}}), profilerInfo.lookups);

        // The lease check alone keeps the unloading ModuleID from the CLR, before a lookup purges its entries.
        handler->RemoveModule(unloadedModuleId);
        tracker.RecordMethod(inlineeModule, 2);
        tracker.RecordModule(handler->RegisterModule(43));
        tracker.TryEnqueue();
        const auto expectedAfterUnload = unloadedModuleId == inlineeModuleId
                                             ? std::vector<NGenPair>{{42, 1}}
                                             : std::vector<NGenPair>{{42, 1}, {43, 1}, {43, 2}};
        EXPECT_EQ(expectedAfterUnload, profilerInfo.lookups);

        tracker.OnModuleUnloading();
        tracker.RecordModule(handler->RegisterModule(44));
        tracker.TryEnqueue();
        const auto expectedAfterPurge = unloadedModuleId == inlineeModuleId
                                            ? std::vector<NGenPair>{{42, 1}}
                                            : std::vector<NGenPair>{{42, 1}, {43, 1}, {43, 2}, {44, 1}, {44, 2}};
        EXPECT_EQ(expectedAfterPurge, profilerInfo.lookups);

        handler->Shutdown();
    }
}

TEST(NGenInlinerTracker, UnloadWaitsForInFlightLookup)
{
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID ngenModuleId = 42;

    // The lookup passes both ModuleIDs to the CLR.
    for (const auto unloadedModuleId : {inlineeModuleId, ngenModuleId})
    {
        SCOPED_TRACE(unloadedModuleId);
        std::promise<void> releaseLookup;
        BlockingNGenTrackerProfilerInfo profilerInfo(releaseLookup.get_future().share());
        auto lookupEnteredFuture = profilerInfo.lookupEntered.get_future();
        auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
        auto handler =
            std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
        NGenInlinerTracker tracker(handler.get());
        tracker.RecordModule(handler->RegisterModule(ngenModuleId));
        tracker.RecordMethod(handler->RegisterModule(inlineeModuleId), 1);

        std::thread lookup([&] { tracker.TryEnqueue(); });
        const auto lookupEntered = lookupEnteredFuture.wait_for(1s) == std::future_status::ready;
        EXPECT_TRUE(lookupEntered);
        if (!lookupEntered)
        {
            releaseLookup.set_value();
            lookup.join();
            handler->Shutdown();
            continue;
        }

        std::promise<void> unloadFinished;
        auto unloadFinishedFuture = unloadFinished.get_future();
        std::thread unload(
            [&]
            {
                handler->RemoveModule(unloadedModuleId);
                unloadFinished.set_value();
            });

        // ModuleUnloadStarted must remain blocked while the CLR can still dereference the module.
        EXPECT_TRUE(WaitUntil([&] { return handler->GetModuleWithLifetime(unloadedModuleId).lifetime == nullptr; }));
        EXPECT_EQ(std::future_status::timeout, unloadFinishedFuture.wait_for(100ms));

        releaseLookup.set_value();
        lookup.join();
        unload.join();

        handler->Shutdown();
    }
}

TEST(NGenInlinerTracker, ReusedModuleIdsAreCheckedFromScratch)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());

    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordModule(handler->RegisterModule(43));
    tracker.RecordMethod(handler->RegisterModule(41), 1);
    tracker.RecordMethod(handler->RegisterModule(44), 2);
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}}), profilerInfo.lookups);

    // The CLR hands the unloaded ModuleIDs to new modules.
    handler->RemoveModule(41);
    tracker.OnModuleUnloading();
    handler->RemoveModule(42);
    tracker.OnModuleUnloading();
    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(handler->RegisterModule(41), 3);
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    WaitForRejitWorker(*handler);

    // NGen module 43 only checks the new method. The new NGen module 42 checks every method still loaded.
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}, {43, 1}, {43, 2}, {43, 3}, {42, 2}, {42, 3}}),
              profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{
                  {{42, 11}, {42, 12}, {43, 11}, {43, 12}}, {{42, 12}, {42, 13}, {43, 13}}}),
              profilerInfo.requestedBatches);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, ContendedLeaseNeverBlocksLookup)
{
    constexpr ModuleID inlineeModuleId = 41;
    constexpr ModuleID ngenModuleId = 42;
    for (const auto contendedModuleId : {inlineeModuleId, ngenModuleId})
    {
        SCOPED_TRACE(contendedModuleId);
        NGenTrackerProfilerInfo profilerInfo;
        auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
        auto handler =
            std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
        NGenInlinerTracker tracker(handler.get());
        tracker.RecordModule(handler->RegisterModule(ngenModuleId));
        tracker.RecordMethod(handler->RegisterModule(inlineeModuleId), 1);

        // An unload waits for a lease held elsewhere.
        const auto contendedModule = handler->GetModuleWithLifetime(contendedModuleId);
        auto heldLease = contendedModule.TryAcquire();
        ASSERT_TRUE(heldLease.has_value());
        std::thread unload([&] { handler->RemoveModule(contendedModuleId); });

        // Only Windows' SRWLOCK makes new readers wait behind a queued writer.
        const auto contended =
            WaitUntil([&] { return !contendedModule.lifetime->TryAcquireNoWait().has_value(); });
#ifdef _WIN32
        EXPECT_TRUE(contended);
#endif

        std::promise<void> lookupReturned;
        auto lookupReturnedFuture = lookupReturned.get_future();
        std::thread lookup(
            [&]
            {
                tracker.TryEnqueue();
                lookupReturned.set_value();
            });
        EXPECT_EQ(std::future_status::ready, lookupReturnedFuture.wait_for(1s));

        heldLease.reset();
        lookup.join();
        unload.join();

        // A contended pair stays pending, and is dropped once its module is unloading.
        tracker.TryEnqueue();
        EXPECT_EQ(contended ? 0u : 1u, profilerInfo.lookups.size());

        handler->Shutdown();
    }
}

TEST(NGenInlinerTracker, CachedFunctionSearchNeverWaitsForAnotherLookup)
{
    std::promise<void> releaseLookup;
    BlockingNGenTrackerProfilerInfo profilerInfo(releaseLookup.get_future().share());
    auto lookupEnteredFuture = profilerInfo.lookupEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    const auto inlineeModule = handler->RegisterModule(41);
    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(inlineeModule, 1);

    std::thread lookup([&] { tracker.TryEnqueue(); });
    const auto lookupEntered = lookupEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(lookupEntered);

    tracker.RecordMethod(inlineeModule, 2);
    std::promise<void> searchReturned;
    auto searchReturnedFuture = searchReturned.get_future();
    std::thread search(
        [&]
        {
            tracker.TryEnqueue();
            searchReturned.set_value();
        });
    EXPECT_EQ(std::future_status::ready, searchReturnedFuture.wait_for(1s));

    releaseLookup.set_value();
    lookup.join();
    search.join();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}}), profilerInfo.lookups);

    // The skipped method stays pending. Once it is checked, nothing is left to look up.
    tracker.TryEnqueue();
    tracker.TryEnqueue();
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}, {42, 2}}), profilerInfo.lookups);

    handler->Shutdown();
}

TEST(NGenInlinerTracker, ModuleLoadWaitsOnlyForCallersNotYetRequested)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    tracker.RecordModule(handler->RegisterModule(42));
    tracker.RecordMethod(handler->RegisterModule(41), 1);

    // Work pending from other loads is left to the next lookup.
    EXPECT_FALSE(tracker.EnqueueAfterModuleLoad(false).has_value());
    EXPECT_TRUE(profilerInfo.lookups.empty());

    auto rejit = tracker.EnqueueAfterModuleLoad(true);
    ASSERT_TRUE(rejit.has_value());
    ASSERT_EQ(std::future_status::ready, rejit->wait_for(1s));
    EXPECT_EQ((std::vector<NGenPair>{{42, 1}}), profilerInfo.lookups);
    EXPECT_EQ((std::vector<std::vector<NGenPair>>{{{42, 11}}}), profilerInfo.requestedBatches);

    // Nothing left to look up and every caller was requested, so the load doesn't wait, even behind a busy worker.
    std::promise<void> releaseWorker;
    ASSERT_TRUE(handler->Enqueue(std::make_unique<RejitWorkItem>(
        [releaseWorkerFuture = releaseWorker.get_future().share()] { releaseWorkerFuture.wait(); })));
    EXPECT_FALSE(tracker.EnqueueAfterModuleLoad(true).has_value());
    EXPECT_EQ(1u, profilerInfo.lookups.size());

    releaseWorker.set_value();
    handler->Shutdown();
}

TEST(NGenInlinerTracker, ModuleLoadDoesNotWaitWithoutInstrumentedMethods)
{
    NGenTrackerProfilerInfo profilerInfo;
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    NGenInlinerTracker tracker(handler.get());
    tracker.RecordModule(handler->RegisterModule(42));

    EXPECT_FALSE(tracker.EnqueueAfterModuleLoad(true).has_value());
    EXPECT_TRUE(profilerInfo.lookups.empty());

    handler->Shutdown();
}

TEST(RejitHandler, RequestRejitKeepsAllModuleGenerationsAliveThroughClrCalls)
{
    std::promise<void> releaseRequest;
    BlockingRejitProfilerInfo profilerInfo(releaseRequest.get_future().share());
    auto requestEnteredFuture = profilerInfo.requestEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader, false);
    constexpr ModuleID firstModuleId = 41;
    constexpr ModuleID secondModuleId = 42;
    handler->RegisterModule(firstModuleId);
    handler->RegisterModule(secondModuleId);

    std::vector<MethodIdentifier> methods{
        {firstModuleId, 1},
        {firstModuleId, 2},
        {secondModuleId, 3},
    };
    auto requests = handler->GetRejitRequests(methods);

    std::thread request([&] { handler->RequestRejit(requests, true); });
    const auto entered = requestEnteredFuture.wait_for(1s) == std::future_status::ready;
    EXPECT_TRUE(entered);

    std::promise<void> unloadFinished;
    auto unloadFinishedFuture = unloadFinished.get_future();
    std::thread unload(
        [&]
        {
            handler->RemoveModule(firstModuleId);
            unloadFinished.set_value();
        });

    const auto moduleUnpublished =
        WaitUntil([&] { return handler->GetModuleWithLifetime(firstModuleId).lifetime == nullptr; });
    EXPECT_TRUE(moduleUnpublished);
    EXPECT_EQ(std::future_status::timeout, unloadFinishedFuture.wait_for(0ms));

    releaseRequest.set_value();
    request.join();
    unload.join();

    EXPECT_EQ(1, profilerInfo.revertCalls);
    EXPECT_EQ(1, profilerInfo.rejitCalls);
    EXPECT_EQ((std::vector<ModuleID>{firstModuleId, firstModuleId, secondModuleId}),
              profilerInfo.revertedModules);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2, 3}), profilerInfo.revertedMethods);
    EXPECT_EQ((std::vector<ModuleID>{firstModuleId, firstModuleId, secondModuleId}),
              profilerInfo.requestedModules);
    EXPECT_EQ((std::vector<mdMethodDef>{1, 2, 3}), profilerInfo.requestedMethods);

    handler->Shutdown();
}

TEST(RejitWorkOffloader, ThrowingWorkItemReleasesWaiterAndLoopSurvives)
{
    MockCorProfilerInfo profilerInfo;
    RejitWorkOffloader offloader(&profilerInfo);

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();
    offloader.Enqueue(std::make_unique<RejitWorkItem>(
        [] { throw std::runtime_error("boom"); },
        [promise] { promise->set_value(); }));

    EXPECT_EQ(std::future_status::ready, future.wait_for(5s));

    auto laterItem = std::make_shared<std::promise<void>>();
    auto laterItemFuture = laterItem->get_future();
    offloader.Enqueue(std::make_unique<RejitWorkItem>([laterItem] { laterItem->set_value(); }));

    EXPECT_EQ(std::future_status::ready, laterItemFuture.wait_for(5s));

    offloader.Enqueue(RejitWorkItem::CreateTerminatingWorkItem());
    offloader.WaitForTermination();
}

TEST(RejitHandlerModule, ConcurrentMetadataCreationPublishesExactlyOneInstance)
{
    constexpr int iterations = 100;

    for (int i = 0; i < iterations; i++)
    {
        RejitHandlerModule module(1, nullptr);
        std::atomic<int> created{0};
        std::atomic<int> ready{0};

        const auto create = [&]
        {
            ready++;
            while (ready < 2)
            {
                std::this_thread::yield();
            }

            module.CreateModuleMetadataIfNotExists(
                [&]
                {
                    created++;
                    return std::make_unique<ModuleMetadata>(
                        ComPtr<IMetaDataImport2>{}, ComPtr<IMetaDataEmit2>{}, ComPtr<IMetaDataAssemblyImport>{},
                        ComPtr<IMetaDataAssemblyEmit>{}, WStr("Assembly"), AppDomainID{}, nullptr, false, false);
                });
        };

        std::thread first(create);
        std::thread second(create);
        first.join();
        second.join();

        ASSERT_EQ(1, created.load());
        ASSERT_NE(nullptr, module.GetModuleMetadata());
    }
}

TEST(DebuggerRejitHandlerModuleMethod, ConcurrentAddRemoveAndReadIsSafe)
{
    debugger::DebuggerRejitHandlerModuleMethod method(mdMethodDefNil, nullptr, FunctionInfo{},
                                                      std::unique_ptr<MethodRewriter>{});
    constexpr int iterations = 1000;
    std::atomic<int> ready{0};
    std::atomic<bool> sawTornProbe{false};

    const auto waitForStart = [&]
    {
        ready++;
        while (ready < 3)
        {
            std::this_thread::yield();
        }
    };

    std::thread adder(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                method.AddProbe(MakeProbe(ProbeId(i)));
            }
        });

    std::thread remover(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                method.RemoveProbe(ProbeId(i));
            }
        });

    std::thread reader(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                for (const auto& probe : method.GetProbes())
                {
                    if (probe == nullptr || probe->probeId.rfind(WStr("probe-"), 0) != 0)
                    {
                        sawTornProbe = true;
                    }
                }
            }
        });

    adder.join();
    remover.join();
    reader.join();

    EXPECT_FALSE(sawTornProbe);
}
