#include "pch.h"

#include "../../src/Datadog.Tracer.Native/module_load_lock.h"

#include <chrono>
#include <future>
#include <vector>

using namespace std::chrono_literals;

namespace trace
{

namespace
{
    enum class ModuleLoadStep
    {
        SnapshotProbes,
        LockModules,
        RunCallbacks
    };

    class RecordingModuleIds
    {
    public:
        class Scope
        {
        public:
            std::vector<ModuleID>& Ref()
            {
                return _modules;
            }

        private:
            friend class RecordingModuleIds;
            explicit Scope(std::vector<ModuleID>& modules) : _modules(modules)
            {
            }

            std::vector<ModuleID>& _modules;
        };

        explicit RecordingModuleIds(std::vector<ModuleLoadStep>& steps) : _steps(steps)
        {
        }

        Scope Get()
        {
            _steps.push_back(ModuleLoadStep::LockModules);
            return Scope(_modules);
        }

    private:
        std::vector<ModuleLoadStep>& _steps;
        std::vector<ModuleID> _modules;
    };
} // namespace

TEST(CorProfilerLockingTest, SnapshotsProbesBeforeAcquiringModuleLock)
{
    std::vector<ModuleLoadStep> steps;
    RecordingModuleIds moduleIds(steps);

    WithModuleLockAfterSnapshot(
        moduleIds,
        [&]() {
            steps.push_back(ModuleLoadStep::SnapshotProbes);
            return 0;
        },
        [&](auto&, const auto&) { steps.push_back(ModuleLoadStep::RunCallbacks); });

    EXPECT_EQ(steps, (std::vector<ModuleLoadStep>{ModuleLoadStep::SnapshotProbes, ModuleLoadStep::LockModules,
                                                  ModuleLoadStep::RunCallbacks}));
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
        WithModuleLockAfterSnapshot(
            moduleIds, []() { return 0; },
            [&](auto&, const auto&) {
                callbacksStarted.set_value();
                finishCallbacksFuture.wait();
            });
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
