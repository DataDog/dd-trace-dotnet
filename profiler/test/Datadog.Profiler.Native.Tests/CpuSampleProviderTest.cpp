// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#ifdef LINUX

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "AppDomainStoreHelper.h"
#include "CpuSampleProvider.h"
#include "FrameStoreHelper.h"
#include "IAppDomainStore.h"
#include "ProfilerMockedInterface.h"
#include "ServiceWrapper.hpp"
#include "SymbolsStore.h"

#include "RingBuffer.h"

TEST(CpuSampleProviderTest, WriteAndReadSample)
{
    auto const maxNbFrames = 4;
    auto appDomainStore = AppDomainStoreHelper(1);
    auto symbolsStore = ServiceWrapper<libdatadog::SymbolsStore>();
    auto frameStore = FrameStoreHelper(true, "Frame", maxNbFrames, symbolsStore);
    MockRuntimeIdStore runtimeIdStore;

    std::string firstExpectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(1))).WillRepeatedly(::testing::Return(firstExpectedRuntimeId.c_str()));

    std::string anotherExpectedRuntimeId = "AnotherRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(2))).WillRepeatedly(::testing::Return(anotherExpectedRuntimeId.c_str()));

    RawSampleTransformer rawSampleTransformer{&frameStore, &appDomainStore, &runtimeIdStore};
    auto valueTypes = SampleValueTypeProvider();

    auto const nbSamples = 11;
    auto metricRegistry = MetricsRegistry();
    auto rb = std::make_unique<RingBuffer>(CpuSampleProvider::SampleSize * nbSamples, CpuSampleProvider::SampleSize);
    auto provider = CpuSampleProvider(valueTypes, &rawSampleTransformer, rb.get(), metricRegistry, symbolsStore);

    provider.Start();

    for (int i = 0; i < nbSamples; i++)
    {
        auto rawSample = provider.GetRawSample();

        rawSample->Duration = std::chrono::nanoseconds(i + 42);
        rawSample->AppDomainId = i % 2 == 0 ? static_cast<AppDomainID>(1) : static_cast<AppDomainID>(2);
        rawSample->LocalRootSpanId = 2 * 10 * i;
        rawSample->SpanId = 3 * 10 * i;
        rawSample->ThreadInfo = nullptr; // No thread info in this test
        rawSample->Timestamp = std::chrono::nanoseconds(i * 20);

        for (auto j = 1; j < maxNbFrames + 1; j++)
        {
            rawSample->Stack.Add(j);
        }
    }
    
    provider.Stop();

    auto samples = provider.GetSamples();

    std::vector<libdatadog::FunctionId*> expectedFunctionIds =
        {
            symbolsStore->InternFunction("Frame #1", "").value(),
            symbolsStore->InternFunction("Frame #2", "").value(),
            symbolsStore->InternFunction("Frame #3", "").value(),
            symbolsStore->InternFunction("Frame #4", "").value(),
        };

    std::vector<libdatadog::ModuleId*> expectedModulesIds =
        {
            symbolsStore->InternMapping("module #1").value(),
            symbolsStore->InternMapping("module #2").value(),
            symbolsStore->InternMapping("module #3").value(),
            symbolsStore->InternMapping("module #4").value(),
        };

    ASSERT_EQ(samples->size(), nbSamples);

    Sample::ValuesCount = 2; // Duration and CPU samples count
    auto sample = std::make_shared<Sample>(0ns, std::string_view{}, 10, symbolsStore);
    size_t currentSampleIdx = 0;
    while (samples->MoveNext(sample))
    {
        size_t currentFrame = 0;
        auto frames = sample->GetCallstack();
        for (auto frame : frames)
        {
            ASSERT_EQ(expectedModulesIds[currentFrame], frame.ModuleId);
            ASSERT_EQ(expectedFunctionIds[currentFrame], frame.FunctionId);

            currentFrame++;
        }

        auto currentTimestamp = std::chrono::nanoseconds(currentSampleIdx * 20);
        ASSERT_EQ(sample->GetTimeStamp(), currentTimestamp);

        ASSERT_EQ(sample->GetValues().size(), 2);

        auto currentDuration = 42 + currentSampleIdx;
        ASSERT_EQ(sample->GetValues()[0], currentDuration);
        ASSERT_EQ(sample->GetValues()[1], 1); // CPU samples count

        auto expectedRuntimeId = (currentSampleIdx % 2 == 0) ? firstExpectedRuntimeId : anotherExpectedRuntimeId;
        ASSERT_EQ(sample->GetRuntimeId(), expectedRuntimeId);
        currentSampleIdx++;
    }
}
#endif // LINUX