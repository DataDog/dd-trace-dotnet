#include "pch.h"

#include "../../../shared/src/native-src/util.h"
#include "../../src/Datadog.Tracer.Native/Synchronized.hpp"
#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/module_rewrite_eligibility.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"

#include <atomic>
#include <chrono>
#include <future>
#include <mutex>
#include <thread>
#include <vector>

using namespace trace;
using namespace std::chrono_literals;

namespace
{
ModuleInfo MakeModuleInfo(DWORD flags, const shared::WSTRING& assemblyName, ModuleID id = 1)
{
    AssemblyInfo assembly(1, assemblyName, 1, 1, WStr("appdomain"));
    return ModuleInfo(id, WStr("C:\\app.dll"), assembly, flags);
}
} // namespace

TEST(ModuleRewriteEligibility, DynamicAndResourceModulesSkipModuleIds)
{
    auto dynamicModule = MakeModuleInfo(COR_PRF_MODULE_DYNAMIC, WStr("DynamicProxyGenAssembly2"));
    auto resourceModule = MakeModuleInfo(COR_PRF_MODULE_RESOURCE, WStr("Customer.Resources"));
    auto winmdModule = MakeModuleInfo(COR_PRF_MODULE_WINDOWS_RUNTIME, WStr("Windows.Foundation"));
    auto invalidModule = ModuleInfo();

    EXPECT_TRUE(ShouldSkipModuleIdsLockForNonRewriteModule(dynamicModule));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(dynamicModule));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(dynamicModule));

    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(resourceModule));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(resourceModule));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(winmdModule));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(invalidModule));
}

TEST(ModuleRewriteEligibility, SkipListsDoNotTakeModuleIdsExceptSpecialCases)
{
    auto mscorlib = MakeModuleInfo(0, WStr("mscorlib"));
    auto corelib = MakeModuleInfo(0, WStr("System.Private.CoreLib"));
    auto loader = MakeModuleInfo(0, WStr("Datadog.Trace.ClrProfiler.Managed.Loader"));
    auto datadogTrace = MakeModuleInfo(0, WStr("Datadog.Trace"));
    auto skipped = MakeModuleInfo(0, WStr("System.Configuration"));
    auto skippedNgen = MakeModuleInfo(COR_PRF_MODULE_NGEN, WStr("System.Configuration"));
    auto customer = MakeModuleInfo(0, WStr("Customer.App"));
    auto included = MakeModuleInfo(0, WStr("Microsoft.Extensions.Logging"));

    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(mscorlib));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(mscorlib));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(corelib));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(corelib));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(loader));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(loader));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(datadogTrace));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(skipped));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForJit(skipped));
    EXPECT_TRUE(ShouldSkipModuleIdsLockForModuleLoad(skippedNgen));
    EXPECT_TRUE(ShouldRegisterModuleLifetime(skippedNgen));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(customer));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForJit(customer));
    EXPECT_TRUE(ShouldRegisterModuleLifetime(customer));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(included));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForJit(included));
}

TEST(ModuleIdsLockProtocol, DynamicModuleLoadDoesNotTakeModuleIdsWhileHeld)
{
    Synchronized<std::vector<ModuleID>> module_ids;
    std::promise<void> releaseHolder;
    std::atomic<bool> holderReady{false};

    std::thread holder(
        [&]
        {
            auto scope = module_ids.Get();
            holderReady = true;
            releaseHolder.get_future().wait();
        });

    while (!holderReady)
    {
        std::this_thread::yield();
    }

    auto dynamicModule = MakeModuleInfo(COR_PRF_MODULE_DYNAMIC, WStr("DynamicProxyGenAssembly2"));
    std::promise<void> skipperDone;

    std::thread skipper(
        [&]
        {
            if (!(ShouldSkipModuleIdsLockForModuleLoad(dynamicModule) && ShouldSkipModuleIdsLockForJit(dynamicModule)))
            {
                auto scope = module_ids.Get();
            }
            skipperDone.set_value();
        });

    const auto skipperStatus = skipperDone.get_future().wait_for(1s);
    releaseHolder.set_value();
    holder.join();
    skipper.join();

    ASSERT_EQ(std::future_status::ready, skipperStatus);
}

TEST(ModuleIdsLockProtocol, InstrumentWaitHappensAfterUnlockAndDoesNotDeadlockModuleLoad)
{
    Synchronized<std::vector<ModuleID>> module_ids;
    std::recursive_mutex probes_mutex;
    std::mutex instrumentation_mutex;
    std::vector<int> probes;

    std::promise<void> rejitGate;
    auto rejitWait = rejitGate.get_future().share();

    std::atomic<bool> locksReleasedBeforeWait{false};
    std::atomic<bool> waitStarted{false};
    std::atomic<bool> waitedAfterUnlock{false};
    std::promise<void> callbackCompleted;
    auto callbackCompletedFuture = callbackCompleted.get_future();

    std::thread instrumentProbes(
        [&]
        {
            std::lock_guard instrumentationLock(instrumentation_mutex);
            {
                std::lock_guard lock(probes_mutex);
                probes.push_back(42);
            }

            auto modulesCopy = module_ids.Copy();
            (void) modulesCopy;

            locksReleasedBeforeWait = true;
            waitStarted = true;
            waitedAfterUnlock = locksReleasedBeforeWait.load();
            rejitWait.wait();
        });

    std::thread moduleLoadFinished(
        [&]
        {
            while (!waitStarted)
            {
                std::this_thread::yield();
            }

            {
                auto modules = module_ids.Get();
                modules.Ref().push_back(7);
            }

            std::vector<int> snapshot;
            {
                std::lock_guard lock(probes_mutex);
                snapshot = probes;
            }

            if (!snapshot.empty())
            {
                callbackCompleted.set_value();
            }
        });

    const auto callbackStatus = callbackCompletedFuture.wait_for(1s);
    rejitGate.set_value();
    instrumentProbes.join();
    moduleLoadFinished.join();

    EXPECT_TRUE(waitedAfterUnlock);
    EXPECT_EQ(std::future_status::ready, callbackStatus);
}

TEST(ModuleIdsLockProtocol, CustomerModuleLoadStillTakesLockAndSeesProbes)
{
    auto customer = MakeModuleInfo(0, WStr("Customer.App"));
    EXPECT_FALSE(ShouldSkipModuleIdsLockForModuleLoad(customer));

    Synchronized<std::vector<ModuleID>> module_ids;
    std::recursive_mutex probes_mutex;
    std::vector<int> probes{1};

    std::atomic<bool> acquiredModuleIds{false};
    std::atomic<bool> appliedProbes{false};

    {
        auto modules = module_ids.Get();
        modules.Ref().push_back(99);
    }

    {
        auto modules = module_ids.Get();
        acquiredModuleIds = shared::Contains(modules.Ref(), static_cast<ModuleID>(99));
    }

    {
        std::lock_guard lock(probes_mutex);
        appliedProbes = !probes.empty();
    }

    EXPECT_TRUE(acquiredModuleIds);
    EXPECT_TRUE(appliedProbes);
}

TEST(ModuleLifetime, UnloadWaitsForActivePreprocessingAndInvalidatesOldSnapshots)
{
    RejitHandler handler(static_cast<ICorProfilerInfo7*>(nullptr), nullptr);
    constexpr ModuleID moduleId = 42;
    handler.RegisterModule(moduleId);

    auto module = handler.GetModuleWithLifetime(moduleId);
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
            handler.RemoveModule(moduleId);
            unloadFinished.set_value();
        });

    unloadStartedFuture.wait();
    EXPECT_EQ(std::future_status::timeout, unloadFinishedFuture.wait_for(50ms));

    moduleLifetime.reset();
    unload.join();

    EXPECT_FALSE(module.Acquire().has_value());

    handler.RegisterModule(moduleId);
    auto reloadedModule = handler.GetModuleWithLifetime(moduleId);
    EXPECT_NE(module.lifetime, reloadedModule.lifetime);
    EXPECT_TRUE(reloadedModule.Acquire().has_value());
}

TEST(ModuleLifetime, ShutdownWaitsForActiveReadersAndInvalidatesOldSnapshots)
{
    RejitHandler handler(static_cast<ICorProfilerInfo7*>(nullptr), nullptr);
    constexpr ModuleID moduleId = 42;
    handler.RegisterModule(moduleId);

    auto module = handler.GetModuleWithLifetime(moduleId);
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
            handler.Shutdown();
            shutdownFinished.set_value();
        });

    shutdownStartedFuture.wait();
    EXPECT_EQ(std::future_status::timeout, shutdownFinishedFuture.wait_for(50ms));

    moduleLifetime.reset();
    shutdown.join();

    EXPECT_FALSE(module.Acquire().has_value());
    EXPECT_FALSE(handler.GetModuleWithLifetime(moduleId).Acquire().has_value());
}

TEST(DebuggerRejitHandlerModuleMethod, DuplicateProbeIdsAreIgnored)
{
    debugger::DebuggerRejitHandlerModuleMethod method(mdMethodDefNil, nullptr, FunctionInfo{},
                                                      std::unique_ptr<MethodRewriter>{});
    auto probe = std::make_shared<debugger::ProbeDefinition>(WStr("probe-id"));

    EXPECT_TRUE(method.AddProbe(probe));
    EXPECT_FALSE(method.AddProbe(probe));
    ASSERT_EQ(1, method.GetProbes().size());

    EXPECT_TRUE(method.RemoveProbe(probe->probeId));
    EXPECT_TRUE(method.GetProbes().empty());
}
