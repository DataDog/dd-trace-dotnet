#include "pch.h"

#include "../../src/Datadog.Tracer.Native/module_load_lock.h"

#include <chrono>
#include <future>
#include <vector>

using namespace std::chrono_literals;

namespace trace
{

TEST(CorProfilerLockingTest, SnapshotsProbesBeforeAcquiringModuleLock)
{
    Synchronized<std::vector<ModuleID>> moduleIds;
    std::promise<void> snapshotStarted;
    auto snapshotStartedFuture = snapshotStarted.get_future();
    std::future<void> moduleLoadFuture;

    {
        auto modules = moduleIds.Get();
        moduleLoadFuture = std::async(std::launch::async, [&]() {
            ModuleLoadLock moduleLock(moduleIds, [&]() {
                snapshotStarted.set_value();
            });
        });

        EXPECT_EQ(snapshotStartedFuture.wait_for(1s), std::future_status::ready);
        EXPECT_EQ(moduleLoadFuture.wait_for(100ms), std::future_status::timeout);
    }

    EXPECT_EQ(moduleLoadFuture.wait_for(1s), std::future_status::ready);
}

TEST(CorProfilerLockingTest, ModuleLockIsHeldDuringModuleCallbacks)
{
    Synchronized<std::vector<ModuleID>> moduleIds;
    std::promise<void> callbacksStarted;
    auto callbacksStartedFuture = callbacksStarted.get_future().share();
    std::promise<void> finishCallbacks;
    auto finishCallbacksFuture = finishCallbacks.get_future().share();
    std::promise<void> unloadStarted;
    auto unloadStartedFuture = unloadStarted.get_future();

    auto moduleLoadFuture = std::async(std::launch::async, [&]() {
        ModuleLoadLock moduleLock(moduleIds, []() {});
        callbacksStarted.set_value();
        finishCallbacksFuture.wait();
    });

    auto moduleUnloadFuture = std::async(std::launch::async, [&]() {
        callbacksStartedFuture.wait();
        unloadStarted.set_value();
        auto modules = moduleIds.Get();
    });

    EXPECT_EQ(unloadStartedFuture.wait_for(1s), std::future_status::ready);
    EXPECT_EQ(moduleUnloadFuture.wait_for(100ms), std::future_status::timeout);
    finishCallbacks.set_value();

    EXPECT_EQ(moduleLoadFuture.wait_for(1s), std::future_status::ready);
    EXPECT_EQ(moduleUnloadFuture.wait_for(1s), std::future_status::ready);
}

} // namespace trace
