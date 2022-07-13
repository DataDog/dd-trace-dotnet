// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <vector>
#include <unordered_map>
#include <chrono>

#include "ProfilerMockedInterface.h"
#include "AppDomainStoreHelper.h"
#include "RuntimeIdStoreHelper.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"
#include "FrameStoreHelper.h"
#include "WallTimeProvider.h"
#include "CpuTimeProvider.h"
#include "RawCpuSample.h"
#include "RawWallTimeSample.h"
#include "ThreadsCpuManagerHelper.h"

using namespace std::chrono_literals;


RawWallTimeSample GetWallTimeRawSample(
    std::uint64_t timeStamp,
    std::uint64_t duration,
    AppDomainID appDomainId,
    std::uint64_t traceId,
    std::uint64_t spanId,
    size_t frameCount
    )
{
    RawWallTimeSample raw;
    raw.Timestamp = timeStamp;
    raw.Duration = duration;
    raw.AppDomainId = appDomainId;
    raw.LocalRootSpanId = traceId;
    raw.SpanId = spanId;

    raw.Stack.reserve(frameCount);
    for (size_t i = 0; i < frameCount; i++)
    {
        raw.Stack.push_back(i + 1); // instruction pointers start at 1 (convention in this test)
    }

    // skip thread info resolution
    raw.ThreadInfo = nullptr;

    return raw;
}

RawCpuSample GetRawCpuSample(
    std::uint64_t timeStamp,
    std::uint64_t duration,
    AppDomainID appDomainId,
    std::uint64_t traceId,
    std::uint64_t spanId,
    size_t frameCount
    )
{
    RawCpuSample raw;
    raw.Timestamp = timeStamp;
    raw.Duration = duration;  // in milliseconds
    raw.AppDomainId = appDomainId;
    raw.LocalRootSpanId = traceId;
    raw.SpanId = spanId;

    raw.Stack.reserve(frameCount);
    for (size_t i = 0; i < frameCount; i++)
    {
        raw.Stack.push_back(i + 1); // instruction pointers start at 1 (convention in this test)
    }

    // skip thread info resolution
    raw.ThreadInfo = nullptr;

    return raw;
}


TEST(WallTimeProviderTest, CheckNoMissingSample)
{
// collect samples and check none are missing on the provider side (just count)
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(2);
    auto threadscpuManager = new ThreadsCpuManagerHelper();
    MockRuntimeIdStore runtimeIdStore;

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(::testing::_)).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    WallTimeProvider provider(threadscpuManager, frameStore, appDomainStore, &runtimeIdStore);
    provider.Start();

    // check the number of samples: 3 here
    provider.Add(RawWallTimeSample());
    provider.Add(RawWallTimeSample());
    provider.Add(RawWallTimeSample());

    auto samples = provider.GetSamples();
    ASSERT_EQ(3, samples.size());

    provider.Stop();
}

TEST(WallTimeProviderTest, CheckAppDomainInfoAndRuntimeId)
{
// add samples and check their appdomain, and pid labels
// Note: thread labels cannot be checked because ThreadInfo is nullptr
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(2);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = new ThreadsCpuManagerHelper();
    MockRuntimeIdStore runtimeIdStore;

    std::string firstExpectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(1))).WillRepeatedly(::testing::Return(firstExpectedRuntimeId.c_str()));

    std::string secondExpectedRuntimeId = "OtherRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(2))).WillRepeatedly(::testing::Return(secondExpectedRuntimeId.c_str()));

    WallTimeProvider provider(threadscpuManager, frameStore, appDomainStore, &runtimeIdStore);
    provider.Start();

    std::vector<size_t> expectedAppDomainId { 1, 2, 2, 1};
    //                                                       V-- check the appdomains are correct
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[0]), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[1]), 0, 0, 2));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[2]), 0, 0, 3));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[3]), 0, 0, 4));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 0;
    for (const Sample& sample : samples)
    {
        const auto& currentRuntimeId = sample.GetRuntimeId();
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

        std::stringstream builder2;
        builder2 << expectedAppDomainId[currentSample];
        std::string expectedPid(builder2.str());

        auto labels = sample.GetLabels();
        for (const Label& label : labels)
        {
            if (label.first == Sample::AppDomainNameLabel)
            {
                ASSERT_EQ(expectedAppDomainName, label.second);
            }
            else if (label.first == Sample::ProcessIdLabel)
            {
                ASSERT_EQ(expectedPid, label.second);
            }
            else
            if (
                (label.first == Sample::ThreadIdLabel) ||
                (label.first == Sample::ThreadNameLabel)
                )
            {
                // can't test thread info
            }
            else
            {
                // unknown label
                ASSERT_TRUE(false);
            }
        }

        currentSample++;
    }
}

TEST(WallTimeProviderTest, CheckFrames)
{
// add samples and check their frames
    auto frameStore = new FrameStoreHelper(true, "Frame", 4);
    auto appDomainStore = new AppDomainStoreHelper(1);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = new ThreadsCpuManagerHelper();
    MockRuntimeIdStore runtimeIdStore;

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(static_cast<AppDomainID>(1))).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    WallTimeProvider provider(threadscpuManager, frameStore, appDomainStore, &runtimeIdStore);
    provider.Start();

    //                                                                 V-- check the frames are correct
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 2));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 3));
    provider.Add(GetWallTimeRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 4));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    std::vector<std::string> expectedFrames =
    {
        "Frame #1",
        "Frame #2",
        "Frame #3",
        "Frame #4",
    };

    std::vector<std::string> expectedModules =
    {
        "module #1",
        "module #2",
        "module #3",
        "module #4",
    };

    for (const Sample& sample : samples)
    {
        size_t currentFrame = 0;
        auto frames = sample.GetCallstack();
        for (auto frame : frames)
        {
            ASSERT_EQ(expectedModules[currentFrame], frame.first);
            ASSERT_EQ(expectedFrames[currentFrame], frame.second);

            currentFrame++;
        }
    }
}

TEST(WallTimeProviderTest, CheckValuesAndTimestamp)
{
    // add samples and check their frames
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(1);
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto threadscpuManager = new ThreadsCpuManagerHelper();
    MockRuntimeIdStore runtimeIdStore;

    std::string expectedRuntimeId = "MyRid";
    EXPECT_CALL(runtimeIdStore, GetId(::testing::_)).WillRepeatedly(::testing::Return(expectedRuntimeId.c_str()));

    WallTimeProvider provider(threadscpuManager, frameStore, appDomainStore, &runtimeIdStore);
    provider.Start();

    //                                V-----V-- check these values are correct
    provider.Add(GetWallTimeRawSample(1000, 10, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(2000, 20, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(3000, 30, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetWallTimeRawSample(4000, 40, static_cast<AppDomainID>(1), 0, 0, 1));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 1;
    for (const Sample& sample : samples)
    {
        ASSERT_EQ(currentSample * 1000, sample.GetTimeStamp());

        auto values = sample.GetValues();
        for (size_t current = 0; current < values.size(); current++)
        {
            if (current == (size_t)SampleValue::WallTimeDuration)
            {
                ASSERT_EQ(currentSample * 10, values[current]);
            }
            else // all other values must be 0
            {
                ASSERT_EQ(0, values[current]);
            }
        }

        currentSample++;
    }
}

TEST(CpuTimeProviderTest, CheckValuesAndTimestamp)
{
    // add samples and check their frames
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(1);
    auto threadscpuManager = new ThreadsCpuManagerHelper();
    RuntimeIdStoreHelper runtimeIdStore;

    CpuTimeProvider provider(threadscpuManager, frameStore, appDomainStore, &runtimeIdStore);
    provider.Start();

    //                           V-----V-- check these values are correct
    provider.Add(GetRawCpuSample(1000, 10, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(2000, 20, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(3000, 30, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawCpuSample(4000, 40, static_cast<AppDomainID>(1), 0, 0, 1));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 1;
    for (const Sample& sample : samples)
    {
        ASSERT_EQ(currentSample * 1000, sample.GetTimeStamp());

        auto values = sample.GetValues();
        for (size_t current = 0; current < values.size(); current++)
        {
            if (current == (size_t)SampleValue::CpuTimeDuration)
            {
                //                             V-- in nanoseconds
                ASSERT_EQ(currentSample * 10 * 1000000, values[current]);
            }
            else // all other values must be 0
            {
                ASSERT_EQ(0, values[current]);
            }
        }

        currentSample++;
    }
}
