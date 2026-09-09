#include "pch.h"

#include "../../src/Datadog.Tracer.Native/Synchronized.hpp"

#include <barrier>
#include <chrono>
#include <future>
#include <mutex>
#include <vector>

using namespace std::chrono_literals;

namespace trace
{

TEST(CorProfilerLockingTest, ModuleLockIsReleasedBeforeDebuggerCallback)
{
    // Model the relevant critical sections using the same synchronization type as CorProfiler::module_ids.
    // InstrumentProbes holds m_probes_mutex while acquiring module_ids. ModuleLoadFinished must therefore
    // release module_ids before its debugger callback attempts to acquire m_probes_mutex.
    Synchronized<std::vector<ModuleID>> moduleIds;
    std::recursive_mutex probesMutex;
    std::barrier firstLocksAcquired(2);

    auto instrumentProbesFuture = std::async(std::launch::async, [&]() {
        std::lock_guard probesLock(probesMutex);
        firstLocksAcquired.arrive_and_wait();
        auto modules = moduleIds.Get();
    });

    auto moduleLoadFuture = std::async(std::launch::async, [&]() {
        {
            auto modules = moduleIds.Get();
            firstLocksAcquired.arrive_and_wait();
        }

        std::lock_guard probesLock(probesMutex);
    });

    EXPECT_EQ(instrumentProbesFuture.wait_for(1s), std::future_status::ready);
    EXPECT_EQ(moduleLoadFuture.wait_for(1s), std::future_status::ready);
}

} // namespace trace
