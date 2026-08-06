#include "pch.h"

#include "mock_cor_profiler_info.h"
#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/iast/dataflow.h"
#include "../../src/Datadog.Tracer.Native/iast/module_info.h"

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
    // callback, where the profiling API rejects the calls resolution needs.
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, PreloadedModulesAreResolvedOnTheNextModuleLoaded)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    dataflow->ModuleLoaded(99);

    // The preloaded module and the newly loaded one, once each.
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);

    // The preloaded list is drained, so a later ModuleLoaded only resolves its own module.
    dataflow->ModuleLoaded(100);
    EXPECT_EQ(3, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, UnloadedModulesAreDroppedFromThePendingPreloadList)
{
    // A preloaded module can unload before the list is drained. Resolving it afterwards would call
    // GetModuleInfo2 on a ModuleID the runtime has already torn down.
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    dataflow->ModuleUnloaded(42);
    dataflow->ModuleLoaded(99);

    // Only the newly loaded module is resolved; the unloaded one is never touched.
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, ModuleLoadedResolvesNewlyLoadedModules)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    dataflow->ModuleLoaded(99);

    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, UnresolvedModulesAreResolvedOnDemand)
{
    // A process may never load another module after Dataflow is created (short-lived apps on .NET
    // Framework), so the preloaded list is never drained. Lookups must still resolve, or those
    // modules would never be instrumented.
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    auto moduleInfo = dataflow->GetModuleInfo(42);

    EXPECT_NE(nullptr, moduleInfo);
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    // Served from cache the second time.
    EXPECT_EQ(moduleInfo, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, FailedResolutionIsNotCachedAndIsRetried)
{
    // A transient failure must not permanently poison the module: the next lookup has to try again.
    MockCorProfilerInfo mockProfiler;
    mockProfiler.getAssemblyInfoFailuresLeft = 1;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    EXPECT_NE(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, GetAspectsModuleDoesNothingWithoutAResolver)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetAspectsModule(1));
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, TwoRuntimesEachResolveTheAspectsModuleThroughTheirOwnProfiler)
{
    // Two runtimes in one process (IIS hosting a .NET Framework app next to an in-process ASP.NET
    // Core one) get one CorProfiler and one Dataflow each. A ModuleID is only meaningful to the
    // runtime that produced it, so each Dataflow must resolve Datadog.Trace.dll through its own
    // owner. Resolving through shared process state hands one runtime the other's ModuleID, which
    // is what faulted in PEAssembly::HasPEImage (APPSEC-69538).
    MockCorProfilerInfo infoA;
    MockCorProfilerInfo infoB;
    auto runtimeInfo = MakeTestRuntimeInformation();

    const ModuleID aspectsModuleInA = 1001;
    const ModuleID aspectsModuleInB = 2002;
    const AppDomainID appDomainId = 7;

    auto dataflowA = new iast::Dataflow([=](AppDomainID) { return aspectsModuleInA; }, &infoA, nullptr,
                                        std::vector<ModuleID>{}, runtimeInfo);
    auto dataflowB = new iast::Dataflow([=](AppDomainID) { return aspectsModuleInB; }, &infoB, nullptr,
                                        std::vector<ModuleID>{}, runtimeInfo);

    auto aspectsInA = dataflowA->GetAspectsModule(appDomainId);
    auto aspectsInB = dataflowB->GetAspectsModule(appDomainId);

    ASSERT_NE(nullptr, aspectsInA);
    ASSERT_NE(nullptr, aspectsInB);

    // Same AppDomainID, different module per runtime: neither borrowed the other's ModuleID.
    EXPECT_EQ(aspectsModuleInA, aspectsInA->_id);
    EXPECT_EQ(aspectsModuleInB, aspectsInB->_id);

    // And each resolved against its own ICorProfilerInfo.
    EXPECT_EQ(1, infoA.getModuleInfo2CallCount);
    EXPECT_EQ(1, infoB.getModuleInfo2CallCount);

    delete dataflowA;
    delete dataflowB;
}

TEST(DataflowTests, NothingIsResolvedWhenTheProfilerQueryInterfaceFailed)
{
    // QI for ICorProfilerInfo3 failing leaves _profiler null and disables Dataflow; resolution must
    // not dereference it.
    MockCorProfilerInfo mockProfiler;
    mockProfiler.failQueryInterface = true;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow({}, &mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    dataflow->ModuleLoaded(99);
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}
