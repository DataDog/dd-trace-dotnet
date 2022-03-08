// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <vector>
#include <unordered_map>
#include <chrono>

#include "ProfilerMockedInterface.h"
#include "AppDomainStoreHelper.h"
#include "IFrameStore.h"
#include "IAppDomainStore.h"
#include "FrameStoreHelper.h"
#include "WallTimeProvider.h"

using namespace std::chrono_literals;


WallTimeSampleRaw GetRawSample(
    std::uint64_t timeStamp,
    std::uint64_t duration,
    AppDomainID appDomainId,
    std::uint64_t traceId,
    std::uint64_t spanId,
    size_t frameCount
    )
{
    WallTimeSampleRaw raw;
    raw.Timestamp = timeStamp;
    raw.Duration = duration;
    raw.AppDomainId = appDomainId;
    raw.TraceId = traceId;
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
    auto [configuration, mockConfiguration] = CreateConfiguration();

    WallTimeProvider provider(configuration.get(), frameStore, appDomainStore);
    provider.Start();

    // check the number of samples: 3 here
    provider.Add(WallTimeSampleRaw());
    provider.Add(WallTimeSampleRaw());
    provider.Add(WallTimeSampleRaw());

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    ASSERT_EQ(3, samples.size());

    provider.Stop();
}

TEST(WallTimeProviderTest, CheckAppDomainInfo)
{
// add samples and check their appdomain, and pid labels
// Note: thread labels cannot be checked because ThreadInfo is nullptr
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(2);
    auto [configuration, mockConfiguration] = CreateConfiguration();

    WallTimeProvider provider(configuration.get(), frameStore, appDomainStore);
    provider.Start();

    std::vector<size_t> expectedAppDomainId { 1, 2, 2, 1};
    //                                                       V-- check the appdomains are correct
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[0]), 0, 0, 1));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[1]), 0, 0, 2));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[2]), 0, 0, 3));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(expectedAppDomainId[3]), 0, 0, 4));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 0;
    for (const Sample& sample : samples)
    {
        std::stringstream builder;
        builder << "AD_" << expectedAppDomainId[currentSample];
        std::string expectedAppDomainName(builder.str());

        std::stringstream builder2;
        builder2 << expectedAppDomainId[currentSample];
        std::string expectedPid(builder2.str());

        auto labels = sample.GetLabels();
        for (const Label& label : labels)
        {
            if (label.first == WallTimeSample::AppDomainNameLabel)
            {
                ASSERT_EQ(expectedAppDomainName, label.second);
            }
            else
            if (label.first == WallTimeSample::ProcessIdLabel)
            {
                ASSERT_EQ(expectedPid, label.second);
            }
            else
            if (
                (label.first == WallTimeSample::ThreadIdLabel) ||
                (label.first == WallTimeSample::ThreadNameLabel)
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

    WallTimeProvider provider(configuration.get(), frameStore, appDomainStore);
    provider.Start();

    //                                                                 V-- check the frames are correct
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 2));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 3));
    provider.Add(GetRawSample(0, 0, static_cast<AppDomainID>(1), 0, 0, 4));

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
    // TODO: add samples and check their frames
    auto frameStore = new FrameStoreHelper(true, "Frame", 1);
    auto appDomainStore = new AppDomainStoreHelper(1);
    auto [configuration, mockConfiguration] = CreateConfiguration();

    WallTimeProvider provider(configuration.get(), frameStore, appDomainStore);
    provider.Start();

    //                        V-----V-- check these values are correct
    provider.Add(GetRawSample(1000, 10, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawSample(2000, 20, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawSample(3000, 30, static_cast<AppDomainID>(1), 0, 0, 1));
    provider.Add(GetRawSample(4000, 40, static_cast<AppDomainID>(1), 0, 0, 1));

    // wait for the provider to collect raw samples
    std::this_thread::sleep_for(200ms);

    auto samples = provider.GetSamples();
    provider.Stop();

    size_t currentSample = 1;
    for (const Sample& sample : samples)
    {
        ASSERT_EQ(currentSample * 1000, sample.GetTimeStamp());

        auto values = sample.GetValues();
        for (const int64_t value : values)
        {
            ASSERT_EQ(currentSample * 10, value);
        }
        currentSample++;
    }
}
