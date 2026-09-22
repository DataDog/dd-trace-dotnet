// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "ManagedThreadList.h"
#include "MetricsRegistry.h"
#include "MockProfilerInfo.h"
#include "ProfilerMockedInterface.h"
#include "StackSamplerLoop.h"
#include "StackSamplerLoopManager.h"
#include "ThreadsCpuManager.h"

using ::testing::Return;

// These tests cover the fix for a shutdown-time SEGV in StackSamplerLoop::CodeHotspotIteration():
// StackSamplerLoop used to be a private implementation detail owned and started/stopped by
// StackSamplerLoopManager via an ad hoc, asynchronous sequence (started from inside a freshly-
// spawned watcher thread, with no synchronization back to the caller). If CorProfilerCallback::
// Shutdown()'s early, explicit Stop() call raced ahead of that async start, it silently no-opped
// (StackSamplerLoop's one-shot ServiceBase CAS guard rejected it), and the sampler thread went on
// to start anyway - running unsupervised, after shutdown had already begun destroying the
// ManagedThreadList instances it depends on.
//
// The fix promotes StackSamplerLoop to its own independently-registered IService, so
// CorProfilerCallback's existing, already-correct StartServices()/StopServices() forward/reverse
// loops govern its lifecycle directly instead of relying on StackSamplerLoopManager to get an
// internal call sequence right by hand. These tests exercise that lifecycle directly, without
// needing a full CorProfilerCallback.

namespace
{
    // Wall-time/CPU profiling are disabled in every test below, so StackSamplerLoop's background
    // thread never selects a target thread to sample - CollectOneThreadStackSample() (the only
    // place that would dereference the StackFramesCollectorBase*/collector pointers) is never
    // reached. That makes it safe to pass nullptr for those collector-related dependencies here;
    // this test is about start/stop lifecycle ordering, not stack-walking.
    void StubOutSamplingConfig(MockConfiguration& config)
    {
        ON_CALL(config, IsWallTimeProfilingEnabled()).WillByDefault(Return(false));
        ON_CALL(config, IsCpuProfilingEnabled()).WillByDefault(Return(false));
        ON_CALL(config, IsInternalMetricsEnabled()).WillByDefault(Return(false));
        ON_CALL(config, WalltimeThreadsThreshold()).WillByDefault(Return(0));
        ON_CALL(config, CpuThreadsThreshold()).WillByDefault(Return(0));
        ON_CALL(config, CodeHotspotsThreadsThreshold()).WillByDefault(Return(0));
        // Avoid a tight busy-spin in StackSamplerLoop::MainLoop() (OpSysTools::Sleep(0ns)) between
        // the Start() and Stop() calls each test issues.
        ON_CALL(config, CpuWallTimeSamplingRate()).WillByDefault(Return(std::chrono::milliseconds(50)));
    }
}

TEST(StackSamplerLoopManagerTest, StartFailsCleanlyWhenStackSamplerLoopWasNeverWiredIn)
{
    MockProfilerInfo profilerInfo;
    ThreadsCpuManager threadsCpuManager;
    MetricsRegistry metricsRegistry;

    StackSamplerLoopManager manager(
        &profilerInfo, nullptr /*metricsSender*/, nullptr /*clrLifetime*/,
        &threadsCpuManager, nullptr /*stackFramesCollector*/, metricsRegistry);

    // SetStackSamplerLoop() is deliberately never called. Before the fix, a mistake like this
    // (e.g. dropping the wiring call in a future refactor of CorProfilerCallback::
    // InitializeServices()) would let RunWatcher() spawn a watcher thread that immediately
    // dereferences a null StackSamplerLoop* - a SIGSEGV on every single startup. StartImpl()'s
    // safety-net check must turn that into a clean, logged Start() failure instead.
    ASSERT_FALSE(manager.Start());
    ASSERT_FALSE(manager.IsStarted());
}

TEST(StackSamplerLoopManagerTest, StackSamplerLoopStartsAndStopsIndependentlyOfTheManager)
{
    MockProfilerInfo profilerInfo;
    auto [configHolder, config] = CreateConfiguration();
    StubOutSamplingConfig(config);

    ThreadsCpuManager threadsCpuManager;
    ManagedThreadList managedThreadList(nullptr);
    ManagedThreadList codeHotspotThreadList(nullptr);
    MetricsRegistry metricsRegistry;

    // The manager is only constructed here because StackSamplerLoop's constructor requires a
    // StackSamplerLoopManager* to notify - it is never Start()ed. Being able to Start()/Stop() the
    // sampler on its own, without the manager's watcher running at all, is exactly the
    // independence this fix is meant to provide (previously, StackSamplerLoop had no lifecycle of
    // its own to test - it was entirely internal to StackSamplerLoopManager).
    StackSamplerLoopManager manager(
        &profilerInfo, nullptr, nullptr, &threadsCpuManager, nullptr, metricsRegistry);

    StackSamplerLoop stackSamplerLoop(
        &profilerInfo, configHolder.get(), nullptr /*stackFramesCollector*/, &manager,
        &threadsCpuManager, &managedThreadList, &codeHotspotThreadList,
        nullptr /*wallTimeCollector*/, nullptr /*cpuTimeCollector*/, metricsRegistry);

    ASSERT_TRUE(stackSamplerLoop.Start());
    ASSERT_TRUE(stackSamplerLoop.IsStarted());
    ASSERT_TRUE(stackSamplerLoop.Stop());
    ASSERT_FALSE(stackSamplerLoop.IsStarted());
}

TEST(StackSamplerLoopManagerTest, FullLifecycleMatchesStartServicesAndStopServicesOrdering)
{
    MockProfilerInfo profilerInfo;
    auto [configHolder, config] = CreateConfiguration();
    StubOutSamplingConfig(config);

    ThreadsCpuManager threadsCpuManager;
    ManagedThreadList managedThreadList(nullptr);
    ManagedThreadList codeHotspotThreadList(nullptr);
    MetricsRegistry metricsRegistry;

    StackSamplerLoopManager manager(
        &profilerInfo, nullptr, nullptr, &threadsCpuManager, nullptr, metricsRegistry);

    StackSamplerLoop stackSamplerLoop(
        &profilerInfo, configHolder.get(), nullptr, &manager, &threadsCpuManager,
        &managedThreadList, &codeHotspotThreadList, nullptr, nullptr, metricsRegistry);

    manager.SetStackSamplerLoop(&stackSamplerLoop);

    // Mirrors CorProfilerCallback::InitializeServices()'s registration order and
    // StartServices()'s plain forward loop: StackSamplerLoopManager is registered (and thus
    // started) before StackSamplerLoop.
    //
    // manager.Start() spawns the watcher thread and blocks - via a std::promise/std::future
    // handshake bounded by a 2s timeout (WatcherStartupTimeout in StackSamplerLoopManager.cpp) -
    // until that thread actually signals it's running, not merely constructed. If that
    // synchronization were broken (e.g. reverted to a bare std::make_unique<std::thread>(...)
    // with no wait), this call would still "succeed" as far as the type system is concerned; what
    // a bug there would actually produce is either this assertion failing once the watcher never
    // signals and the 2s timeout elapses, or a flaky downstream failure under scheduler
    // contention - which is exactly the class of bug that caused the original crash. A clean,
    // prompt pass here is the signal that the handshake is intact.
    ASSERT_TRUE(manager.Start());

    // StackSamplerLoop::Start() is no longer triggered asynchronously from inside the watcher
    // thread - it's called directly, synchronously, by whoever starts the services in sequence
    // (StartServices() in production, this test here). That's the structural fix: there is no
    // longer an async gap in which a Stop() call could race ahead of it.
    ASSERT_TRUE(stackSamplerLoop.Start());
    ASSERT_TRUE(stackSamplerLoop.IsStarted());

    // Mirrors StopServices()'s existing REVERSE loop: StackSamplerLoop (registered later) is
    // stopped before StackSamplerLoopManager (registered earlier). This keeps the watcher alive
    // while the sampler's background thread is being joined (preserving the deadlock-rescue
    // behavior), and guarantees the sampler is fully stopped before anything it depends on
    // (ManagedThreadList/CodeHotspotsThreadList, both registered even earlier in production) can
    // be destroyed.
    ASSERT_TRUE(stackSamplerLoop.Stop());
    ASSERT_FALSE(stackSamplerLoop.IsStarted());

    ASSERT_TRUE(manager.Stop());
    ASSERT_FALSE(manager.IsStarted());
}
