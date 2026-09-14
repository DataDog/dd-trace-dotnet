#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"

#include <atomic>
#include <chrono>
#include <future>
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
