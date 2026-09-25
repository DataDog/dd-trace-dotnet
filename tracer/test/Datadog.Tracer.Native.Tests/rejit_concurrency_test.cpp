#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"
#include "../../src/Datadog.Tracer.Native/tracer_rejit_preprocessor.h"
#include "mock_cor_profiler_info.h"

#include <atomic>
#include <chrono>
#include <future>
#include <thread>
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
        handler(std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader))
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

class ObservableTracerRejitPreprocessor : public TracerRejitPreprocessor
{
public:
    using TracerRejitPreprocessor::TracerRejitPreprocessor;

    std::promise<void> removeEntered;
    std::promise<void> removeReturned;

    void RemoveModule(ModuleID moduleId) override
    {
        removeEntered.set_value();
        TracerRejitPreprocessor::RemoveModule(moduleId);
        removeReturned.set_value();
    }
};
} // namespace

TEST(ModuleLifetime, UnloadWaitsForActiveLeaseAndInvalidatesSnapshots)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    const auto module = handler->RegisterModule(moduleId);
    EXPECT_EQ(module.lifetime, handler->RegisterModule(moduleId).lifetime);

    auto moduleLifetime = module.Acquire();
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

    EXPECT_FALSE(module.Acquire().has_value());

    handler->RegisterModule(moduleId);
    const auto reloadedModule = handler->GetModuleWithLifetime(moduleId);
    EXPECT_NE(module.lifetime, reloadedModule.lifetime);
    EXPECT_TRUE(reloadedModule.Acquire().has_value());
}

TEST(ModuleLifetime, ShutdownWaitsForActiveLeaseAndInvalidatesModules)
{
    RejitHandlerContext context;
    const auto& handler = context.handler;
    constexpr ModuleID moduleId = 42;
    handler->RegisterModule(moduleId);

    const auto module = handler->GetModuleWithLifetime(moduleId);
    auto moduleLifetime = module.Acquire();
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

    EXPECT_FALSE(module.Acquire().has_value());
    EXPECT_FALSE(handler->GetModuleWithLifetime(moduleId).Acquire().has_value());

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
    EXPECT_FALSE(module.Acquire().has_value());

    releaseBlocker.set_value();
    shutdown.join();
}

TEST(ModuleLifetime, UnloadWaitsForNGenReplayAfterShutdownStarts)
{
    std::promise<void> releaseEnumeration;
    BlockingNGenProfilerInfo profilerInfo(releaseEnumeration.get_future().share());
    auto enumerationEnteredFuture = profilerInfo.enumerationEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader);
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

TEST(RejitHandlerShutdown, AcceptedWorkCompletesBeforeTerminator)
{
    RejitHandlerContext context;
    const auto& offloader = context.offloader;
    const auto& handler = context.handler;

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

    auto completed = std::make_shared<std::promise<void>>();
    auto completedFuture = completed->get_future();
    EXPECT_TRUE(handler->Enqueue(std::make_unique<RejitWorkItem>(
        [completed]() mutable { completed->set_value(); })));

    std::thread shutdown([&] { handler->Shutdown(); });
    EXPECT_TRUE(WaitUntil([&] { return handler->IsShutdownRequested(); }));

    releaseBlocker.set_value();
    EXPECT_EQ(std::future_status::ready, completedFuture.wait_for(1s));
    shutdown.join();
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
    EXPECT_FALSE(oldRequests[0].Acquire().has_value());

    handler->RequestRejit(oldRequests);
    EXPECT_EQ(0, context.profilerInfo.requestRejitCallCount);
}

TEST(RejitHandler, RequestRejitKeepsAllModuleGenerationsAliveThroughClrCalls)
{
    std::promise<void> releaseRequest;
    BlockingRejitProfilerInfo profilerInfo(releaseRequest.get_future().share());
    auto requestEnteredFuture = profilerInfo.requestEntered.get_future();
    auto offloader = std::make_shared<RejitWorkOffloader>(&profilerInfo);
    auto handler = std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(&profilerInfo), offloader);
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
