#include "pch.h"

#include "../../src/Datadog.Tracer.Native/Synchronized.hpp"

#include <barrier>
#include <chrono>
#include <future>
#include <mutex>
#include <thread>

using namespace std::chrono_literals;

namespace trace
{

TEST(SynchronizedTest, TryGetBlocksWhenLockIsHeld)
{
    Synchronized<int> synchronized;
    std::promise<void> attemptStarted;
    auto attemptStartedFuture = attemptStarted.get_future();
    std::future<bool> tryGetResult;

    {
        auto heldScope = synchronized.Get();
        tryGetResult = std::async(std::launch::async, [&]() {
            attemptStarted.set_value();
            return synchronized.TryGet().has_value();
        });

        ASSERT_EQ(attemptStartedFuture.wait_for(1s), std::future_status::ready);
        EXPECT_EQ(tryGetResult.wait_for(100ms), std::future_status::timeout);
    }

    ASSERT_EQ(tryGetResult.wait_for(1s), std::future_status::ready);
    EXPECT_TRUE(tryGetResult.get());
}

TEST(SynchronizedTest, ReleasingModuleLockBeforeDebuggerCallbackAvoidsDeadlock)
{
    // This models the production paths with barriers at the point where the old implementation deadlocked:
    //
    // InstrumentProbes:    m_probes_mutex -> module_ids mutex
    // ModuleLoadFinished:  module_ids mutex -> release module_ids -> m_probes_mutex
    //
    std::recursive_mutex probesMutex;
    std::mutex moduleIdsMutex;
    std::barrier firstLocksAcquired(2);

    auto instrumentProbesFuture = std::async(std::launch::async, [&]() {
        std::lock_guard probesLock(probesMutex);
        firstLocksAcquired.arrive_and_wait();
        std::lock_guard modulesLock(moduleIdsMutex);
    });

    auto moduleLoadFuture = std::async(std::launch::async, [&]() {
        {
            std::lock_guard modulesLock(moduleIdsMutex);
            firstLocksAcquired.arrive_and_wait();
        }

        std::lock_guard probesLock(probesMutex);
    });

    EXPECT_EQ(instrumentProbesFuture.wait_for(1s), std::future_status::ready);
    EXPECT_EQ(moduleLoadFuture.wait_for(1s), std::future_status::ready);
}

} // namespace trace
