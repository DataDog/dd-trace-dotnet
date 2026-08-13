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

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

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

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

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

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);
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

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    auto moduleInfo = dataflow->GetModuleInfo(42);

    EXPECT_NE(nullptr, moduleInfo);
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    // Served from cache the second time.
    EXPECT_EQ(moduleInfo, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, ResolvingAModuleOnlyAsksForReadAccess)
{
    // Opening a module's metadata for writing makes the runtime materialize a writable copy, swapping
    // out the PEImage the running code was built against. Resolving a module must not do that: we
    // resolve every module that loads, and we read Datadog.Trace.dll's metadata to look up aspects
    // while its own code is running, which faulted in PEAssembly::HasPEImage (APPSEC-69538).
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, std::vector<ModuleID>{42}, runtimeInfo);

    dataflow->ModuleLoaded(99);
    dataflow->GetModuleInfo(100);

    EXPECT_FALSE(mockProfiler.moduleMetaDataOpenFlags.empty());
    EXPECT_FALSE(mockProfiler.AskedForWriteAccess());
    for (auto flags : mockProfiler.moduleMetaDataOpenFlags)
    {
        EXPECT_EQ(static_cast<DWORD>(ofRead), flags);
    }

    delete dataflow;
}

TEST(DataflowTests, FailedResolutionIsCachedAndNotRetried)
{
    // GetModuleInfo is reached from the JIT callbacks, so a module that cannot be resolved must not
    // be re-attempted on every JIT: that would mean thousands of CLR calls and error log lines for
    // one module. The failure modes here are permanent for a given ModuleID (unloading module, dead
    // id, Windows Runtime), so the failure is cached.
    MockCorProfilerInfo mockProfiler;
    mockProfiler.getAssemblyInfoFailuresLeft = 1;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    // Served from the cached failure even though the mock would now succeed.
    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, UnloadingAModuleWithACachedFailureDoesNotDereferenceIt)
{
    // A cached failure stores a null entry; ModuleUnloaded logs the module name, so it must not
    // dereference it (this crashed with debug logging enabled).
    MockCorProfilerInfo mockProfiler;
    mockProfiler.getAssemblyInfoFailuresLeft = 1;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(S_OK, dataflow->ModuleUnloaded(42));

    delete dataflow;
}

TEST(DataflowTests, NothingIsResolvedWhenTheProfilerQueryInterfaceFailed)
{
    // QI for ICorProfilerInfo3 failing leaves _profiler null and disables Dataflow; resolution must
    // not dereference it.
    MockCorProfilerInfo mockProfiler;
    mockProfiler.failQueryInterface = true;
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&mockProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    dataflow->ModuleLoaded(99);
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}
