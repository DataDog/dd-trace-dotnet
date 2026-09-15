#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"

#include <atomic>
#include <chrono>
#include <future>
#include <stdexcept>
#include <thread>
#include <vector>

using namespace trace;
using namespace std::chrono_literals;

namespace
{
// RejitHandler only touches the profiler info and the work offloader through paths that are guarded by the
// shutdown flag, so the lifetime protocol can be driven without either of them.
std::unique_ptr<RejitHandler> MakeHandler()
{
    return std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(nullptr), nullptr);
}

debugger::ProbeDefinition_S MakeProbe(const shared::WSTRING& probeId)
{
    return std::make_shared<debugger::ProbeDefinition>(shared::WSTRING(probeId));
}

shared::WSTRING ProbeId(int index)
{
    return WStr("probe-") + shared::ToWSTRING(static_cast<uint64_t>(index));
}
} // namespace

TEST(ModuleLifetime, UnloadWaitsForActivePreprocessingAndInvalidatesOldSnapshots)
{
    auto handler = MakeHandler();
    constexpr ModuleID moduleId = 42;
    handler->RegisterModule(moduleId);

    auto module = handler->GetModuleWithLifetime(moduleId);
    auto moduleLifetime = module.Acquire();
    ASSERT_TRUE(moduleLifetime.has_value());

    std::promise<void> unloadStarted;
    std::promise<void> unloadFinished;
    auto unloadStartedFuture = unloadStarted.get_future();
    auto unloadFinishedFuture = unloadFinished.get_future();
    std::thread unload(
        [&]
        {
            unloadStarted.set_value();
            handler->RemoveModule(moduleId);
            unloadFinished.set_value();
        });

    unloadStartedFuture.wait();
    EXPECT_EQ(std::future_status::timeout, unloadFinishedFuture.wait_for(50ms));

    moduleLifetime.reset();
    unload.join();

    EXPECT_FALSE(module.Acquire().has_value());

    handler->RegisterModule(moduleId);
    auto reloadedModule = handler->GetModuleWithLifetime(moduleId);
    EXPECT_NE(module.lifetime, reloadedModule.lifetime);
    EXPECT_TRUE(reloadedModule.Acquire().has_value());
}

TEST(ModuleLifetime, ShutdownWaitsForActiveReadersAndInvalidatesOldSnapshots)
{
    auto handler = MakeHandler();
    constexpr ModuleID moduleId = 42;
    handler->RegisterModule(moduleId);

    auto module = handler->GetModuleWithLifetime(moduleId);
    auto moduleLifetime = module.Acquire();
    ASSERT_TRUE(moduleLifetime.has_value());

    std::promise<void> shutdownStarted;
    std::promise<void> shutdownFinished;
    auto shutdownStartedFuture = shutdownStarted.get_future();
    auto shutdownFinishedFuture = shutdownFinished.get_future();
    std::thread shutdown(
        [&]
        {
            shutdownStarted.set_value();
            handler->Shutdown();
            shutdownFinished.set_value();
        });

    shutdownStartedFuture.wait();
    EXPECT_EQ(std::future_status::timeout, shutdownFinishedFuture.wait_for(50ms));

    moduleLifetime.reset();
    shutdown.join();

    EXPECT_FALSE(module.Acquire().has_value());
    EXPECT_FALSE(handler->GetModuleWithLifetime(moduleId).Acquire().has_value());
}

// Preprocessing acquires one lifetime per module in a batch, and the debugger acquires the same module's
// lifetime concurrently from the managed thread. Neither may exclude the other.
TEST(ModuleLifetime, ConcurrentAcquireDoesNotSerializeReaders)
{
    auto handler = MakeHandler();
    constexpr ModuleID moduleId = 7;
    handler->RegisterModule(moduleId);

    auto module = handler->GetModuleWithLifetime(moduleId);
    auto first = module.Acquire();
    ASSERT_TRUE(first.has_value());

    std::promise<bool> secondAcquired;
    auto secondAcquiredFuture = secondAcquired.get_future();
    std::thread reader([&] { secondAcquired.set_value(module.Acquire().has_value()); });

    ASSERT_EQ(std::future_status::ready, secondAcquiredFuture.wait_for(1s));
    EXPECT_TRUE(secondAcquiredFuture.get());
    reader.join();
}

TEST(ModuleLifetime, UnregisteredModuleCannotBeAcquired)
{
    auto handler = MakeHandler();

    auto module = handler->GetModuleWithLifetime(99);
    EXPECT_EQ(nullptr, module.lifetime);
    EXPECT_FALSE(module.Acquire().has_value());
}

TEST(ModuleLifetime, GetModulesWithLifetimeReturnsOnlyRegisteredModules)
{
    auto handler = MakeHandler();
    handler->RegisterModule(1);
    handler->RegisterModule(3);

    const auto modules = handler->GetModulesWithLifetime({1, 2, 3});

    ASSERT_EQ(2u, modules.size());
    EXPECT_EQ(1u, modules[0].id);
    EXPECT_EQ(3u, modules[1].id);
    EXPECT_TRUE(modules[0].Acquire().has_value());
    EXPECT_TRUE(modules[1].Acquire().has_value());
}

TEST(ModuleLifetime, RegisterModuleIsIdempotent)
{
    auto handler = MakeHandler();
    handler->RegisterModule(5);
    const auto first = handler->GetModuleWithLifetime(5);

    handler->RegisterModule(5);
    const auto second = handler->GetModuleWithLifetime(5);

    EXPECT_EQ(first.lifetime, second.lifetime);
}

// CorProfiler::Shutdown is reachable from the CLR callback and from the DisableTracerCLRProfiler export.
// RejitHandler::Shutdown has to tolerate that without joining the worker thread twice.
TEST(RejitHandlerShutdown, IsIdempotent)
{
    auto handler = MakeHandler();
    handler->RegisterModule(1);

    handler->Shutdown();
    EXPECT_TRUE(handler->IsShutdownRequested());

    handler->Shutdown();
    EXPECT_TRUE(handler->IsShutdownRequested());
}

TEST(RejitHandlerShutdown, RemoveModuleAfterShutdownIsANoOp)
{
    auto handler = MakeHandler();
    handler->RegisterModule(1);
    handler->Shutdown();

    handler->RemoveModule(1);

    EXPECT_FALSE(handler->GetModuleWithLifetime(1).Acquire().has_value());
}

TEST(RejitHandlerShutdown, RegisterModuleAfterShutdownIsRejected)
{
    auto handler = MakeHandler();
    handler->Shutdown();

    handler->RegisterModule(1);

    EXPECT_EQ(nullptr, handler->GetModuleWithLifetime(1).lifetime);
}

// Every enqueue path resolves its promise when the work is refused, otherwise the managed caller blocks forever
// on the future (APMS-20456).
TEST(RejitHandlerShutdown, EnqueueIsRefusedAfterShutdown)
{
    auto handler = MakeHandler();
    handler->Shutdown();

    EXPECT_FALSE(handler->Enqueue(std::make_unique<RejitWorkItem>([] {})));
}

TEST(RejitHandlerShutdown, EnqueueForRejitResolvesPromiseAfterShutdown)
{
    auto handler = MakeHandler();
    handler->Shutdown();

    std::vector<ModuleID> modules{1};
    std::vector<mdMethodDef> methods{2};
    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();

    handler->EnqueueForRejit(modules, methods, promise);

    EXPECT_EQ(std::future_status::ready, future.wait_for(1s));
}

// A throwing work item must still release whoever is waiting on it. The caller keeps its own shared_ptr to
// the promise (it needs it for the enqueue-refused path), so the promise object outlives the work item and
// an unresolved promise is a permanent hang, not a broken_promise.
TEST(RejitWorkOffloader, ThrowingWorkItemReleasesItsWaiterAndTheLoopSurvives)
{
    RejitWorkOffloader offloader(nullptr);

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();

    offloader.Enqueue(std::make_unique<RejitWorkItem>(
        [] { throw std::runtime_error("boom"); },
        [localPromise = promise]() mutable { localPromise->set_value(); }));

    EXPECT_EQ(std::future_status::ready, future.wait_for(5s));

    // The worker must still be alive and processing after the failure.
    auto laterItem = std::make_shared<std::promise<void>>();
    auto laterItemFuture = laterItem->get_future();
    offloader.Enqueue(std::make_unique<RejitWorkItem>([laterItem]() mutable { laterItem->set_value(); }));

    EXPECT_EQ(std::future_status::ready, laterItemFuture.wait_for(5s));

    offloader.Enqueue(RejitWorkItem::CreateTerminatingWorkItem());
    offloader.WaitForTermination();
}

// An item that throws *after* resolving its promise must not trip promise_already_satisfied by way of the
// release path.
TEST(RejitWorkOffloader, ItemThrowingAfterResolvingIsTolerated)
{
    RejitWorkOffloader offloader(nullptr);

    auto promise = std::make_shared<std::promise<void>>();
    auto future = promise->get_future();

    offloader.Enqueue(std::make_unique<RejitWorkItem>(
        [localPromise = promise]() mutable
        {
            localPromise->set_value();
            throw std::runtime_error("boom");
        },
        [localPromise = promise]() mutable { localPromise->set_value(); }));

    EXPECT_EQ(std::future_status::ready, future.wait_for(5s));

    auto laterItem = std::make_shared<std::promise<void>>();
    auto laterItemFuture = laterItem->get_future();
    offloader.Enqueue(std::make_unique<RejitWorkItem>([laterItem]() mutable { laterItem->set_value(); }));

    EXPECT_EQ(std::future_status::ready, laterItemFuture.wait_for(5s));

    offloader.Enqueue(RejitWorkItem::CreateTerminatingWorkItem());
    offloader.WaitForTermination();
}

// Shutdown sets the flag and enqueues the terminator under the same write lock that Enqueue reads it under,
// so a successful enqueue must land before the terminator and a refused one must be reported to the caller.
// Either way the caller has to end up released — never queued behind a terminator that already passed.
TEST(RejitHandlerShutdown, EnqueueRacingShutdownAlwaysReleasesCallers)
{
    constexpr int iterations = 25;
    constexpr int itemsPerThread = 40;

    for (int i = 0; i < iterations; i++)
    {
        auto offloader = std::make_shared<RejitWorkOffloader>(nullptr);
        auto handler = std::make_unique<RejitHandler>(static_cast<ICorProfilerInfo7*>(nullptr), offloader);

        std::atomic<int> ready{0};
        std::atomic<int> unreleased{0};
        const auto waitForStart = [&] { ready++; while (ready < 2) {} };

        std::thread producer(
            [&]
            {
                waitForStart();
                for (int item = 0; item < itemsPerThread; item++)
                {
                    auto promise = std::make_shared<std::promise<void>>();
                    auto future = promise->get_future();

                    // Mirrors every enqueue-with-promise path: the work resolves the promise, and if the
                    // handler refuses the item the caller resolves it instead.
                    if (!handler->Enqueue(std::make_unique<RejitWorkItem>(
                            [promise]() mutable { promise->set_value(); },
                            [promise]() mutable { promise->set_value(); })))
                    {
                        promise->set_value();
                    }

                    if (future.wait_for(5s) != std::future_status::ready)
                    {
                        unreleased++;
                    }
                }
            });

        std::thread shutdown(
            [&]
            {
                waitForStart();
                handler->Shutdown();
            });

        producer.join();
        shutdown.join();

        ASSERT_EQ(0, unreleased.load());
    }
}

// CorProfiler::Shutdown races the CLR callbacks that consult the handler. None of them may crash, hang, or
// observe a half-torn-down handler, in either interleaving. Covers RemoveModule and RegisterModule racing
// Shutdown, which the lock protocol proves but nothing exercised.
TEST(RejitHandlerShutdown, ConcurrentCallbacksDuringShutdownAreSafe)
{
    constexpr int iterations = 50;
    constexpr ModuleID moduleCount = 8;

    for (int i = 0; i < iterations; i++)
    {
        auto handler = MakeHandler();
        for (ModuleID moduleId = 1; moduleId <= moduleCount; moduleId++)
        {
            handler->RegisterModule(moduleId);
        }

        std::atomic<int> ready{0};
        const auto waitForStart = [&] { ready++; while (ready < 4) {} };

        // Stands in for JITInlining / JITCachedFunctionSearchStarted.
        std::thread callbacks(
            [&]
            {
                waitForStart();
                for (ModuleID moduleId = 1; moduleId <= moduleCount; moduleId++)
                {
                    handler->HasModuleAndMethod(moduleId, mdMethodDefNil);
                    handler->AddNGenInlinerModule(moduleId);
                    handler->HasBeenRejitted(moduleId, mdMethodDefNil);
                }
            });

        // Stands in for ModuleUnloadStarted racing the teardown.
        std::thread unloads(
            [&]
            {
                waitForStart();
                for (ModuleID moduleId = 1; moduleId <= moduleCount; moduleId++)
                {
                    handler->RemoveModule(moduleId);
                }
            });

        // Stands in for ModuleLoadFinished still arriving while the tracer is being disabled.
        std::thread loads(
            [&]
            {
                waitForStart();
                for (ModuleID moduleId = moduleCount + 1; moduleId <= moduleCount * 2; moduleId++)
                {
                    handler->RegisterModule(moduleId);
                }
            });

        std::thread shutdown(
            [&]
            {
                waitForStart();
                handler->Shutdown();
            });

        callbacks.join();
        unloads.join();
        loads.join();
        shutdown.join();

        ASSERT_TRUE(handler->IsShutdownRequested());

        // Shutdown must leave nothing acquirable, including anything registered while it was running.
        for (ModuleID moduleId = 1; moduleId <= moduleCount * 2; moduleId++)
        {
            ASSERT_FALSE(handler->GetModuleWithLifetime(moduleId).Acquire().has_value());
        }
    }
}

// A module lifetime is a shared lease, so two preprocessors can reach the same module at once. Creating the
// metadata has to be a single create-if-absent: with a plain check-then-set both threads allocate and the
// loser's assignment deletes the instance a concurrent rewrite is still reading through.
TEST(RejitHandlerModule, ConcurrentMetadataCreationPublishesExactlyOneInstance)
{
    constexpr int iterations = 200;

    for (int i = 0; i < iterations; i++)
    {
        RejitHandlerModule module(1, nullptr);
        std::atomic<int> created{0};
        std::atomic<int> ready{0};

        const auto create = [&]
        {
            // Line both threads up so they contend on the same create-if-absent.
            ready++;
            while (ready < 2)
            {
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

TEST(DebuggerRejitHandlerModuleMethod, DuplicateProbeIdsAreIgnored)
{
    debugger::DebuggerRejitHandlerModuleMethod method(mdMethodDefNil, nullptr, FunctionInfo{},
                                                      std::unique_ptr<MethodRewriter>{});
    auto probe = MakeProbe(WStr("probe-id"));

    EXPECT_TRUE(method.AddProbe(probe));
    EXPECT_FALSE(method.AddProbe(probe));
    ASSERT_EQ(1u, method.GetProbes().size());

    EXPECT_TRUE(method.RemoveProbe(probe->probeId));
    EXPECT_TRUE(method.GetProbes().empty());
}

// Probes are added by the ReJIT worker, removed by the managed thread, and read by whichever thread the runtime
// calls GetReJITParameters on. Readers must always observe a self-consistent snapshot.
TEST(DebuggerRejitHandlerModuleMethod, ConcurrentAddRemoveAndReadIsSafe)
{
    debugger::DebuggerRejitHandlerModuleMethod method(mdMethodDefNil, nullptr, FunctionInfo{},
                                                      std::unique_ptr<MethodRewriter>{});
    constexpr int iterations = 2000;
    std::atomic<bool> sawTornProbe{false};

    std::thread adder(
        [&]
        {
            for (int i = 0; i < iterations; i++)
            {
                method.AddProbe(MakeProbe(ProbeId(i)));
            }
        });

    std::thread remover(
        [&]
        {
            for (int i = 0; i < iterations; i++)
            {
                method.RemoveProbe(ProbeId(i));
            }
        });

    std::thread reader(
        [&]
        {
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
