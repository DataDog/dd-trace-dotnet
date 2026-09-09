#include "pch.h"

#include "../../src/Datadog.Tracer.Native/Synchronized.hpp"

#include <chrono>
#include <future>
#include <mutex>
#include <vector>

using namespace std::chrono_literals;

namespace trace
{

TEST(CorProfilerLockingTest, SnapshottingProbesBeforeModuleLockAvoidsLockInversion)
{
    Synchronized<std::vector<ModuleID>> moduleIds;
    std::recursive_mutex probesMutex;
    std::promise<void> probesLocked;
    auto probesLockedFuture = probesLocked.get_future().share();
    std::promise<void> snapshotStarted;
    auto snapshotStartedFuture = snapshotStarted.get_future();
    std::promise<void> continueInstrumentProbes;
    auto continueInstrumentProbesFuture = continueInstrumentProbes.get_future().share();

    auto instrumentProbesFuture = std::async(std::launch::async, [&]() {
        std::lock_guard probesLock(probesMutex);
        probesLocked.set_value();
        continueInstrumentProbesFuture.wait();
        auto modules = moduleIds.Get();
    });

    auto moduleLoadFuture = std::async(std::launch::async, [&]() {
        {
            probesLockedFuture.wait();
            snapshotStarted.set_value();
            std::lock_guard probesLock(probesMutex);
        }

        auto modules = moduleIds.Get();
    });

    EXPECT_EQ(snapshotStartedFuture.wait_for(1s), std::future_status::ready);
    continueInstrumentProbes.set_value();

    EXPECT_EQ(instrumentProbesFuture.wait_for(1s), std::future_status::ready);
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
        auto modules = moduleIds.Get();
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
