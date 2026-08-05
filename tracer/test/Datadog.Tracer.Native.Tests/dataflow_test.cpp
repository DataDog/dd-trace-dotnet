#include "pch.h"

#include "mock_cor_profiler_info.h"
#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/iast/dataflow.h"

using namespace trace;

namespace
{
RuntimeInformation MakeTestRuntimeInformation()
{
    return RuntimeInformation(COR_PRF_DESKTOP_CLR, 4, 0, 0, 0);
}
} // namespace

TEST(DataflowTests, PreloadedModulesAreResolvedEagerlyAtConstruction)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    // A later lookup for the same module must be served from cache, not trigger a new call.
    auto moduleInfo = dataflow->GetModuleInfo(42);
    EXPECT_NE(nullptr, moduleInfo);
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, ModuleLoadedStillResolvesNewlyLoadedModules)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    dataflow->ModuleLoaded(99);

    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}
