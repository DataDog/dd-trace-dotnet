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

TEST(DataflowTests, PreloadedModulesAreNotResolvedFromTheConstructor)
{
    // The constructor runs on the RegisterIastAspects P/Invoke thread, outside any profiler
    // callback, where the profiling API rejects the calls ResolveModuleInfo needs. Nothing may be
    // resolved from here.
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);
    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));

    delete dataflow;
}

TEST(DataflowTests, PreloadedModulesAreResolvedOnTheNextModuleLoaded)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    dataflow->ModuleLoaded(99);

    // The preloaded module and the newly loaded one, once each.
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);
    EXPECT_NE(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_NE(nullptr, dataflow->GetModuleInfo(99));

    // The preloaded list is drained, so a later ModuleLoaded only resolves its own module.
    dataflow->ModuleLoaded(100);
    EXPECT_EQ(3, mockProfiler.getModuleInfo2CallCount);

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
