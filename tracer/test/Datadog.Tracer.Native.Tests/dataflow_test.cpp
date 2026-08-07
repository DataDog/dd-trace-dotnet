#include "pch.h"

#include "mock_cor_profiler_info.h"
#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/cor_profiler.h"
#include "../../src/Datadog.Tracer.Native/iast/dataflow.h"
#include "../../src/Datadog.Tracer.Native/iast/module_info.h"

using namespace trace;

namespace
{
RuntimeInformation MakeTestRuntimeInformation()
{
    return RuntimeInformation(COR_PRF_DESKTOP_CLR, 4, 0, 0, 0);
}

// Stands in for the runtime that owns a Dataflow: hands it a mock ICorProfilerInfo and reports a
// fixed aspects module (0 meaning Datadog.Trace.dll is not loaded).
class FakeCorProfiler : public CorProfiler
{
public:
    explicit FakeCorProfiler(MockCorProfilerInfo* info, ModuleID aspectsModuleId = 0) :
        _info(info), _aspectsModuleId(aspectsModuleId)
    {
    }

    ICorProfilerInfo* GetCorProfilerInfo() override
    {
        return _info;
    }

    ModuleID GetProfilerAssemblyModuleId(AppDomainID) override
    {
        return _aspectsModuleId;
    }

    std::vector<ModuleID> GetProfilerAssemblyModuleIds() override
    {
        if (_aspectsModuleId == 0)
        {
            return {};
        }

        return {_aspectsModuleId};
    }

private:
    MockCorProfilerInfo* _info;
    ModuleID _aspectsModuleId;
};
} // namespace

TEST(DataflowTests, PreloadedModulesAreNotResolvedFromTheConstructor)
{
    // The constructor runs on the RegisterIastAspects P/Invoke thread, outside any profiler
    // callback, where the profiling API rejects the calls resolution needs.
    MockCorProfilerInfo mockProfiler;
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, PreloadedModulesAreResolvedOnTheNextModuleLoaded)
{
    MockCorProfilerInfo mockProfiler;
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

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
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    dataflow->ModuleUnloaded(42);
    dataflow->ModuleLoaded(99);

    // Only the newly loaded module is resolved; the unloaded one is never touched.
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, ModuleLoadedResolvesNewlyLoadedModules)
{
    MockCorProfilerInfo mockProfiler;
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);
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
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    auto moduleInfo = dataflow->GetModuleInfo(42);

    EXPECT_NE(nullptr, moduleInfo);
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    // Served from cache the second time.
    EXPECT_EQ(moduleInfo, dataflow->GetModuleInfo(42));
    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

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
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

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
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(S_OK, dataflow->ModuleUnloaded(42));

    delete dataflow;
}

TEST(DataflowTests, GetAspectsModuleDoesNothingWhenTheAspectsModuleIsNotLoaded)
{
    // Datadog.Trace.dll is not loaded in this AppDomain yet, so the owner reports no ModuleID.
    MockCorProfilerInfo mockProfiler;
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetAspectsModule(1));
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, TheAspectsModuleIsPreloadedFromTheCorProfiler)
{
    // Datadog.Trace.dll's own module is deliberately excluded from the module list the profiler
    // tracks, so Dataflow adds it to the preloaded list itself. Without that, GetAspectsModule would
    // never find it in _modules and no aspect could be defined.
    MockCorProfilerInfo mockProfiler;
    FakeCorProfiler corProfiler(&mockProfiler, 1001);
    auto runtimeInfo = MakeTestRuntimeInformation();

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, std::vector<ModuleID>{}, runtimeInfo);

    dataflow->ModuleLoaded(99);

    // The aspects module was drained alongside the newly loaded one...
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);

    // ...and is served from the cache afterwards.
    EXPECT_NE(nullptr, dataflow->GetModuleInfo(1001));
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);

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

    FakeCorProfiler corProfilerA(&infoA, aspectsModuleInA);
    FakeCorProfiler corProfilerB(&infoB, aspectsModuleInB);

    auto dataflowA = new iast::Dataflow(&corProfilerA, nullptr, std::vector<ModuleID>{}, runtimeInfo);
    auto dataflowB = new iast::Dataflow(&corProfilerB, nullptr, std::vector<ModuleID>{}, runtimeInfo);

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

TEST(DataflowTests, NothingIsResolvedWithoutAnOwningCorProfiler)
{
    // Without an owner there is no ICorProfilerInfo to talk to either, so Dataflow stays disabled
    // instead of dereferencing anything.
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(nullptr, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    EXPECT_EQ(nullptr, dataflow->GetAspectsModule(1));
    EXPECT_EQ(S_OK, dataflow->ModuleLoaded(99));

    delete dataflow;
}

TEST(DataflowTests, NothingIsResolvedWhenTheProfilerQueryInterfaceFailed)
{
    // QI for ICorProfilerInfo3 failing leaves _profiler null and disables Dataflow; resolution must
    // not dereference it.
    MockCorProfilerInfo mockProfiler;
    mockProfiler.failQueryInterface = true;
    FakeCorProfiler corProfiler(&mockProfiler);
    auto runtimeInfo = MakeTestRuntimeInformation();
    std::vector<ModuleID> preloadedModules{42};

    auto dataflow = new iast::Dataflow(&corProfiler, nullptr, preloadedModules, runtimeInfo);

    EXPECT_EQ(nullptr, dataflow->GetModuleInfo(42));
    dataflow->ModuleLoaded(99);
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}
