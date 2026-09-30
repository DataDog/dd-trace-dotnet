#include "pch.h"

#include "../../src/Datadog.Tracer.Native/debugger_rejit_handler_module_method.h"
#include "../../src/Datadog.Tracer.Native/rejit_handler.h"

#include <atomic>
#include <thread>

using namespace trace;

namespace
{
debugger::ProbeDefinition_S MakeProbe(const shared::WSTRING& probeId)
{
    return std::make_shared<debugger::ProbeDefinition>(shared::WSTRING(probeId));
}

shared::WSTRING ProbeId(int index)
{
    return WStr("probe-") + shared::ToWSTRING(static_cast<uint64_t>(index));
}
} // namespace

TEST(RejitHandlerModule, ConcurrentMetadataCreationPublishesExactlyOneInstance)
{
    constexpr int iterations = 100;

    for (int i = 0; i < iterations; i++)
    {
        RejitHandlerModule module(1, nullptr);
        std::atomic<int> created{0};
        std::atomic<int> ready{0};

        const auto create = [&]
        {
            ready++;
            while (ready < 2)
            {
                std::this_thread::yield();
            }

            module.CreateModuleMetadataIfNotExists(
                [&]
                {
                    created++;
                    return std::make_unique<ModuleMetadata>(
                        ComPtr<IMetaDataImport2>{}, ComPtr<IMetaDataEmit2>{}, ComPtr<IMetaDataAssemblyImport>{},
                        ComPtr<IMetaDataAssemblyEmit>{}, WStr("Assembly"), AppDomainID{}, nullptr, false, false);
                });
        };

        std::thread first(create);
        std::thread second(create);
        first.join();
        second.join();

        ASSERT_EQ(1, created.load());
        ASSERT_NE(nullptr, module.GetModuleMetadata());
    }
}

TEST(DebuggerRejitHandlerModuleMethod, ConcurrentAddRemoveAndReadIsSafe)
{
    debugger::DebuggerRejitHandlerModuleMethod method(mdMethodDefNil, nullptr, FunctionInfo{},
                                                      std::unique_ptr<MethodRewriter>{});
    constexpr int iterations = 1000;
    std::atomic<int> ready{0};
    std::atomic<bool> sawTornProbe{false};

    const auto waitForStart = [&]
    {
        ready++;
        while (ready < 3)
        {
            std::this_thread::yield();
        }
    };

    std::thread adder(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                method.AddProbe(MakeProbe(ProbeId(i)));
            }
        });

    std::thread remover(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                method.RemoveProbe(ProbeId(i));
            }
        });

    std::thread reader(
        [&]
        {
            waitForStart();
            for (int i = 0; i < iterations; i++)
            {
                for (const auto& probe : method.GetProbes())
                {
                    if (probe == nullptr || probe->probeId.rfind(WStr("probe-"), 0) != 0)
                    {
                        sawTornProbe = true;
                    }
                }
            }
        });

    adder.join();
    remover.join();
    reader.join();

    EXPECT_FALSE(sawTornProbe);
}
