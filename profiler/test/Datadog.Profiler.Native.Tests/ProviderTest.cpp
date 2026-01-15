// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

//#include "gtest/gtest.h"

#include <chrono>
#include <memory>
#include <unordered_map>
#include <vector>

#include "AppDomainStoreHelper.h"
#include "CallstackProvider.h"
#include "CpuTimeProvider.h"
#include "FrameStoreHelper.h"
#include "IAppDomainStore.h"
#include "IFrameStore.h"
#include "MemoryResourceManager.h"
#include "OpSysTools.h"
#include "ProfilerMockedInterface.h"
#include "RawCpuSample.h"
#include "RawSampleTransformer.h"
#include "RawWallTimeSample.h"
#include "RuntimeIdStoreHelper.h"
#include "SampleValueTypeProvider.h"
#include "ThreadsCpuManagerHelper.h"
#include "WallTimeProvider.h"
#include "ServiceWrapper.hpp"
#include "SymbolsStore.h"

#include "shared/src/native-src/dd_memory_resource.hpp"

#define INTERN_MODULE(m)                                                \
    auto module##m##Id = symbolsStore->InternMapping("module #" #m);                       \
    if (!module##m##Id)                                                         \
    {                                                                   \
        ASSERT_TRUE(false) << "Failed to intern module '" << #m << "'"; \
    }

#define INTERN_FUNCTION(fn)                                                 \
    auto frame##fn##Id = symbolsStore->InternFunction("Frame #" #fn, "");                    \
    if (!frame##fn##Id)                                                            \
    {                                                                       \
        ASSERT_TRUE(false) << " Failed to intern function '" << #fn << "'"; \
    }

#define INTERN_STRING(s)                                                \
    auto s##Id = symbolsStore->InternString(#s);                        \
    if (!s##Id)                                                         \
    {                                                                   \
        ASSERT_TRUE(false) << "Failed to intern string '" << #s << "'"; \
    }


using namespace std::chrono_literals;

CallstackProvider callstackProvider(MemoryResourceManager::GetDefault());

RawWallTimeSample GetWallTimeRawSample(
    std::chrono::nanoseconds timeStamp,
    std::chrono::nanoseconds duration,
    AppDomainID appDomainId,
    std::uint64_t traceId,
    std::uint64_t spanId,
    size_t frameCount)
{
    RawWallTimeSample raw;
    raw.Timestamp = timeStamp;
    raw.Duration = duration;
    raw.AppDomainId = appDomainId;
    raw.LocalRootSpanId = traceId;
    raw.SpanId = spanId;

    raw.Stack = callstackProvider.Get();
    for (size_t i = 0; i < frameCount; i++)
    {
        raw.Stack.Add(i + 1); // instruction pointers start at 1 (convention in this test)
    }

    // skip thread info resolution
    raw.ThreadInfo = nullptr;

    return raw;
}

RawCpuSample GetRawCpuSample(
    std::chrono::nanoseconds timeStamp,
    std::chrono::nanoseconds duration,
    AppDomainID appDomainId,
    std::uint64_t traceId,
    std::uint64_t spanId,
    size_t frameCount)
{
    RawCpuSample raw;
    raw.Timestamp = timeStamp;
    raw.Duration = duration; // in milliseconds
    raw.AppDomainId = appDomainId;
    raw.LocalRootSpanId = traceId;
    raw.SpanId = spanId;

    raw.Stack = callstackProvider.Get();
    for (size_t i = 0; i < frameCount; i++)
    {
        raw.Stack.Add(i + 1); // instruction pointers start at 1 (convention in this test)
    }

    // skip thread info resolution
    raw.ThreadInfo = nullptr;

    return raw;
}

TEST(WallTimeProviderTest, CheckNoMissingSample)
{
    // collect samples and check none are missing on the provider side (just count)
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", 1, symbolsStore);
    auto appDomainStore = AppDomainStoreHelper(2);
    auto threadscpuManager = ThreadsCpuManagerHelper();
    auto valueTypeProvider = SampleValueTypeProvider();
    MockRuntimeIdStore runtimeIdStore;
    auto [configuration, mockConfiguration] = CreateConfiguration();

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(::testing::_)).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    WallTimeProvider provider(valueTypeProvider, &rawSampleTransformer, shared::pmr::get_default_resource(), symbolsStore);
    Sample::ValuesCount = 1;
    provider.Start();

    // check the number of samples: 3 here
    provider.Add(RawWallTimeSample());
    provider.Add(RawWallTimeSample());
    provider.Add(RawWallTimeSample());

    auto samples = provider.GetSamples();
    ASSERT_EQ(3, samples->size());

    provider.Stop();
}

TEST(WallTimeProviderTest, CheckAppDomainInfoAndRuntimeId)
{
    // add samples and check their appdomain, and pid labels
    // Note: thread labels cannot be checked because ThreadInfo is nullptr
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", 1, symbolsStore);
    auto appDomainStore = AppDomainStoreHelper(2);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = ThreadsCpuManagerHelper();
    auto valueTypeProvider = SampleValueTypeProvider();
    MockRuntimeIdStore runtimeIdStore;

    std::string firstExpectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(1))).WillRepeatedly(::testing::Return(firstExpectedRuntimeId.c_str()));

    std::string secondExpectedRuntimeId = "OtherRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(2))).WillRepeatedly(::testing::Return(secondExpectedRuntimeId.c_str()));

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    WallTimeProvider provider(valueTypeProvider, &rawSampleTransformer, shared::pmr::get_default_resource(), symbolsStore);
    Sample::ValuesCount = 1;
    provider.Start();

    std::vector<size_t> expectedAppDomainId{1, 2, 2, 1};
    //                                                       V-- check the appdomains are correct
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(expectedAppDomainId[0]), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(expectedAppDomainId[1]), 0, 0, 2));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(expectedAppDomainId[2]), 0, 0, 3));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(expectedAppDomainId[3]), 0, 0, 4));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 0;
    auto sample = std::make_shared<Sample>(0ns, std::string_view{}, 10, symbolsStore);

    auto expectedPid = OpSysTools::GetProcId();
    while (samples->MoveNext(sample))
    {
        const auto& currentRuntimeId = sample->GetRuntimeId();
        if (expectedAppDomainId[currentSample] == 1)
        {
            ASSERT_EQ(currentRuntimeId, firstExpectedRuntimeId);
        }
        else
        {
            ASSERT_EQ(currentRuntimeId, secondExpectedRuntimeId);
        }

        std::stringstream builder;
        builder << "AD_" << expectedAppDomainId[currentSample];
        std::string expectedAppDomainName(builder.str());

        auto expectedPid = expectedAppDomainId[currentSample];

        auto pidLabelId = symbolsStore->GetProcessId();
        auto appDomainLabelId = symbolsStore->GetAppDomainName();
        auto threadLabelId = symbolsStore->GetThreadId();
        auto threadNameLabelId = symbolsStore->GetThreadName();
        auto labels = sample->GetLabels();
        for (auto const& label : labels)
        {
            std::visit(LabelsVisitor{
                [expectedPid, pidLabelId](NumericLabel const& label){
                    auto const& [name, value] = label;
                    if(name == pidLabelId)
                    {
                        ASSERT_EQ(expectedPid, value);
                    }
                    else
                    {
                        ASSERT_TRUE(false) << label.first;
                    }
                },
                [expectedAppDomainName, appDomainLabelId, threadLabelId, threadNameLabelId](StringLabel const& label) {
                    auto const& [name, value] = label;
                    if (name == appDomainLabelId)
                    {
                        ASSERT_EQ(expectedAppDomainName, value);
                    }
                    else if (
                        (name == threadLabelId) ||
                        (name == threadNameLabelId))
                    {
                        // can't test thread info
                    }
                    else
                    {
                        // unknown label
                        ASSERT_TRUE(false);
                    }
                }, 
            }, label);
        }

        currentSample++;
    }
}

TEST(WallTimeProviderTest, CheckFrames)
{
    // add samples and check their frames
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", 4, symbolsStore);
    auto appDomainStore = AppDomainStoreHelper(1);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = ThreadsCpuManagerHelper();
    auto valueTypeProvider = SampleValueTypeProvider();
    MockRuntimeIdStore runtimeIdStore;

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(1))).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    WallTimeProvider provider(valueTypeProvider, &rawSampleTransformer, shared::pmr::get_default_resource(), symbolsStore);
    Sample::ValuesCount = 1;
    provider.Start();

    //                                                                 V-- check the frames are correct
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(1), 0, 0, 2));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(1), 0, 0, 3));
    provider.Add(GetWallTimeRawSample(0ns, 0ns, static_cast<AppDomainID>(1), 0, 0, 4));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    INTERN_FUNCTION(1);
    INTERN_FUNCTION(2);
    INTERN_FUNCTION(3);
    INTERN_FUNCTION(4);

    std::vector<libdatadog::FunctionId*> expectedFrames =
        {
            frame1Id.value(),
            frame2Id.value(),
            frame3Id.value(),
            frame4Id.value(),
        };

    INTERN_MODULE(1);
    INTERN_MODULE(2);
    INTERN_MODULE(3);
    INTERN_MODULE(4);
    std::vector<libdatadog::ModuleId*> expectedModules =
        {
            module1Id.value(),
            module2Id.value(),
            module3Id.value(),
            module4Id.value(),
        };

    auto sample = std::make_shared<Sample>(0ns, std::string_view{}, 10, symbolsStore);

    while (samples->MoveNext(sample))
    {
        size_t currentFrame = 0;
        auto frames = sample->GetCallstack();
        for (auto frame : frames)
        {
            ASSERT_EQ(expectedModules[currentFrame], frame.ModuleId);
            ASSERT_EQ(expectedFrames[currentFrame], frame.FunctionId);

            currentFrame++;
        }
    }
}

TEST(WallTimeProviderTest, CheckValuesAndTimestamp)
{
    // add samples and check their frames
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", 1, symbolsStore);
    auto appDomainStore = AppDomainStoreHelper(1);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = ThreadsCpuManagerHelper();
    auto valueTypeProvider = SampleValueTypeProvider();
    MockRuntimeIdStore runtimeIdStore;

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(::testing::_)).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    WallTimeProvider provider(valueTypeProvider, &rawSampleTransformer, shared::pmr::get_default_resource(), symbolsStore);
    Sample::ValuesCount = 1;
    provider.Start();

    //                                V-----V-- check these values are correct
    provider.Add(GetWallTimeRawSample(1000ns, 10ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(2000ns, 20ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(3000ns, 30ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(4000ns, 40ns, static_cast<AppDomainID>(1), 0, 0, 1));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    auto currentSample = 1ns;
    auto sample = std::make_shared<Sample>(0ns, std::string_view{}, 10, symbolsStore);

    while (samples->MoveNext(sample))
    {
        ASSERT_EQ(currentSample * 1000, sample->GetTimeStamp());

        auto values = sample->GetValues();
        ASSERT_EQ(values.size(), 1);
        for (size_t current = 0; current < values.size(); current++)
        {
            ASSERT_EQ(currentSample * 10, std::chrono::nanoseconds(values[current]));
        }

        currentSample++;
    }
}

TEST(CpuTimeProviderTest, CheckValuesAndTimestamp)
{
    // add samples and check their frames
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", 1, symbolsStore);
    auto appDomainStore = AppDomainStoreHelper(1);
    auto threadscpuManager = ThreadsCpuManagerHelper();
    auto valueTypeProvider = SampleValueTypeProvider();
    RuntimeIdStoreHelper runtimeIdStore;
    auto [configuration, mockConfiguration] = CreateConfiguration();

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    CpuTimeProvider provider(valueTypeProvider, &rawSampleTransformer, shared::pmr::get_default_resource(), symbolsStore);
    Sample::ValuesCount = 2;
    provider.Start();

    //                           V-----V-- check these values are correct
    provider.Add(GetRawCpuSample(1000ns, 10ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(2000ns, 20ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(3000ns, 30ns, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(4000ns, 40ns, static_cast<AppDomainID>(1), 0, 0, 1));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    ASSERT_EQ(4, samples->size());
    provider.Stop();

    auto currentSample = 1ns;
    auto sample = std::make_shared<Sample>(0ns, std::string_view{}, 10, symbolsStore);

    while (samples->MoveNext(sample))
    {
        ASSERT_EQ(currentSample * 1000, sample->GetTimeStamp());

        auto values = sample->GetValues();
        ASSERT_EQ(values.size(), 2);
        ASSERT_EQ(currentSample * 10, std::chrono::nanoseconds(values[0]));
        ASSERT_GT(values[1], 0);

        currentSample++;
    }
}


extern "C"
{
    #include "datadog/common.h"
    #include "datadog/profiling.h"
}
TEST(CpuTimeProviderTest, XX)
{

    ddog_prof_ProfilesDictionaryHandle dict = {0};
    auto status = ddog_prof_ProfilesDictionary_new(&dict);
    if (status.err != nullptr)
    {
        ASSERT_FALSE(true) << "Failed to create dict";
    }

    auto fn = ddog_prof_Function2{
        .name = DDOG_PROF_STRINGID2_EMPTY,
        .system_name = DDOG_PROF_STRINGID2_EMPTY,
        .file_name = DDOG_PROF_STRINGID2_EMPTY};

    ddog_prof_FunctionId2 function_id;
    status = ddog_prof_ProfilesDictionary_insert_function(&function_id, dict, &fn);
    if (status.err != nullptr)
    {
        ASSERT_FALSE(true) << "Failed to intern function";
    }
}
