#include "pch.h"

#include "mock_cor_profiler_info.h"
#include "../../src/Datadog.Tracer.Native/clr_helpers.h"
#include "../../src/Datadog.Tracer.Native/iast/dataflow.h"
#include "../../src/Datadog.Tracer.Native/iast/module_info.h"

#ifdef _WIN32
#include "test_helpers.h"
#include "../../src/Datadog.Tracer.Native/iast/dataflow_aspects.h"

#include <chrono>
#include <functional>
#include <future>
#endif

using namespace trace;

namespace
{
RuntimeInformation MakeTestRuntimeInformation()
{
    return RuntimeInformation(COR_PRF_DESKTOP_CLR, 4, 0, 0, 0);
}

ModuleIDWithLifetime LiveModule(ModuleID moduleId)
{
    return {moduleId, std::make_shared<ModuleLifetime>()};
}

#ifdef _WIN32
constexpr mdMemberRef aspectTarget = 0x0A000001;

// Resolves every module to the Samples.ExampleLibrary metadata and gives every method a body that calls
// aspectTarget once.
class RewritableMethodProfilerInfo : public MockCorProfilerInfo
{
public:
    ComPtr<IMetaDataImport2> metadataImport;
    // Tiny header for a 6-byte body: call aspectTarget; ret.
    const BYTE methodBody[7] = {(6 << 2) | CorILMethod_TinyFormat, 0x28, 0x01, 0x00, 0x00, 0x0A, 0x2A};
    std::function<void()> onRequestReJIT;

    HRESULT STDMETHODCALLTYPE GetModuleMetaData(ModuleID moduleId, DWORD dwOpenFlags, const IID& riid,
                                                IUnknown** ppOut) override
    {
        return metadataImport->QueryInterface(riid, reinterpret_cast<void**>(ppOut));
    }

    HRESULT STDMETHODCALLTYPE GetILFunctionBody(ModuleID moduleId, mdMethodDef methodId, LPCBYTE* ppMethodHeader,
                                                ULONG* pcbMethodSize) override
    {
        *ppMethodHeader = methodBody;
        if (pcbMethodSize != nullptr)
        {
            *pcbMethodSize = sizeof(methodBody);
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE RequestReJIT(ULONG cFunctions, ModuleID moduleIds[], mdMethodDef methodIds[]) override
    {
        requestRejitCallCount++;
        onRequestReJIT();
        return S_OK;
    }
};

class TestAspectClass : public iast::DataflowAspectClass
{
public:
    explicit TestAspectClass(iast::Dataflow* dataflow) : DataflowAspectClass(dataflow)
    {
    }
};

// No parameter shifts, so Apply reports the target call as instrumented without importing an aspect method,
// which would need the aspects module and the global profiler.
class TestAspect : public iast::DataflowAspect
{
public:
    explicit TestAspect(iast::DataflowAspectClass* aspectClass) : DataflowAspect(aspectClass)
    {
    }
};

class AspectInjectingDataflow : public iast::Dataflow
{
public:
    using iast::Dataflow::Dataflow;

    void AddAspect(ModuleID moduleId, iast::DataflowAspect* aspect, mdMemberRef target)
    {
        auto moduleAspects = new iast::ModuleAspects(this, GetModuleInfo(moduleId));
        moduleAspects->_aspects.push_back(new iast::DataflowAspectReference(moduleAspects, aspect, target, 0, {}));
        _moduleAspects[moduleId] = moduleAspects;
    }
};
#endif
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

    dataflow->ModuleLoaded(LiveModule(99));

    // The preloaded module and the newly loaded one, once each.
    EXPECT_EQ(2, mockProfiler.getModuleInfo2CallCount);

    // The preloaded list is drained, so a later ModuleLoaded only resolves its own module.
    dataflow->ModuleLoaded(LiveModule(100));
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
    dataflow->ModuleLoaded(LiveModule(99));

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

    dataflow->ModuleLoaded(LiveModule(99));

    EXPECT_EQ(1, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}

TEST(DataflowTests, StaleGenerationDoesNotResolveModule)
{
    MockCorProfilerInfo mockProfiler;
    auto runtimeInfo = MakeTestRuntimeInformation();
    auto offloader = std::make_shared<RejitWorkOffloader>(&mockProfiler);
    auto handler =
        std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&mockProfiler), offloader);
    auto dataflow = std::make_unique<iast::Dataflow>(&mockProfiler, handler, std::vector<ModuleID>{}, runtimeInfo);

    const auto module = handler->RegisterModule(99);
    handler->RemoveModule(module.id);

    EXPECT_EQ(S_FALSE, dataflow->ModuleLoaded(module));
    EXPECT_FALSE(dataflow->JITCompilationStarted(module, 1));
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    handler->Shutdown();
}

#ifdef _WIN32
// Windows only: the metadata comes from the .NET Framework metadata dispenser (CLRHelperTestBase).
class DataflowMetadataTests : public CLRHelperTestBase
{
};

TEST_F(DataflowMetadataTests, InliningDecisionRequestsRejitAfterReleasingTheDataflowLock)
{
    // NotifyReJITParameters waits for Dataflow's lock while holding the module's lifetime lease. Requesting the
    // ReJIT, which takes that lease, while still holding Dataflow's lock deadlocks once an unload queues for the
    // lease: SRWLOCK makes the new reader wait behind the writer, which waits for the first lease holder.
    RewritableMethodProfilerInfo mockProfiler;
    mockProfiler.metadataImport = metadata_import_;
    auto runtimeInfo = MakeTestRuntimeInformation();
    auto offloader = std::make_shared<RejitWorkOffloader>(&mockProfiler);
    auto handler =
        std::make_shared<RejitHandler>(static_cast<ICorProfilerInfo7*>(&mockProfiler), offloader);
    auto dataflow =
        std::make_unique<AspectInjectingDataflow>(&mockProfiler, handler, std::vector<ModuleID>{}, runtimeInfo);

    const auto module = handler->RegisterModule(99);
    TestAspectClass aspectClass(dataflow.get());
    TestAspect aspect(&aspectClass);
    dataflow->AddAspect(module.id, &aspect, aspectTarget);

    // _cs is recursive, so only another thread can tell whether the requesting thread still holds it. Declared
    // after dataflow: its destructor waits for the probe, which may still be blocked on _cs when the check fails.
    std::future<void> lockProbe;
    bool lockFreeAtRequestReJIT = false;
    mockProfiler.onRequestReJIT = [&] {
        lockProbe = std::async(std::launch::async, [&] { dataflow->GetModuleInfo(module.id); });
        lockFreeAtRequestReJIT = lockProbe.wait_for(std::chrono::seconds(1)) == std::future_status::ready;
    };

    const auto callee = FunctionToTest(WStr("Samples.ExampleLibrary.Class1"), WStr("Add"));
    EXPECT_FALSE(dataflow->IsInlineEnabled(module.id, callee.id));
    EXPECT_EQ(1, mockProfiler.requestRejitCallCount);
    EXPECT_TRUE(lockFreeAtRequestReJIT);

    handler->Shutdown();
}
#endif

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

    dataflow->ModuleLoaded(LiveModule(99));
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
    dataflow->ModuleLoaded(LiveModule(99));
    EXPECT_EQ(0, mockProfiler.getModuleInfo2CallCount);

    delete dataflow;
}
