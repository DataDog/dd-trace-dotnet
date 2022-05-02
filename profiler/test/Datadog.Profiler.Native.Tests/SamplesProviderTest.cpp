// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <sstream>
#include <thread>

#include "Sample.h"
#include "ProviderBase.h"


class TestSamplesProvider : public ProviderBase
{
public:
    void Add(Sample&& sample)
    {
        Store(std::move(sample));
    }
};

Sample GetTestSample(std::string_view runtimeId, const std::string& framePrefix, const std::string& labelId, const std::string& labelValue)
{
    Sample sample{runtimeId};
    // wall values
    sample.AddValue(100, SampleValue::WallTimeDuration);
    sample.AddValue(200, SampleValue::WallTimeDuration);
    sample.AddValue(300, SampleValue::WallTimeDuration);
    // cpu values
    sample.AddValue(300, SampleValue::CpuTimeDuration);
    sample.AddValue(200, SampleValue::CpuTimeDuration);
    sample.AddValue(100, SampleValue::CpuTimeDuration);
    // --> only the last one should be kept

    Label l;
    l.first = labelId;
    l.second = labelValue;
    sample.AddLabel(l);

    sample.AddFrame("module", framePrefix + " #1");
    sample.AddFrame("module", framePrefix + " #2");
    sample.AddFrame("module", framePrefix + " #3");

    return sample;
}

void ValidateTestSample(const Sample& sample, const std::string& framePrefix, const std::string& labelId, const std::string& labelValue)
{
    // Check values
    // Today, only 2 value in the array
    //    WallTime
    //    CpuTime
    // --> should be increased when a new profiler is added
    //     this is a good reminder to add dedicated tests  :^)
    auto values = sample.GetValues();
    ASSERT_EQ(2, values.size());

    for (size_t current = 0; current < 2; current++)
    {
        // for the same SampleValue, only the last "added" value is kept
        // update GetTestSample() for new profilers
        if (current == (size_t)SampleValue::WallTimeDuration)
        {
            ASSERT_EQ(300, values[current]);
        }
        else
        if (current == (size_t)SampleValue::CpuTimeDuration)
        {
            ASSERT_EQ(100, values[current]);
        }
        else
        {
            FAIL();
        }
    }

    // check labels
    auto labels = sample.GetLabels();
    ASSERT_EQ(1, labels.size());
    auto label = labels.front();
    ASSERT_TRUE(label.first == labelId);
    ASSERT_TRUE(label.second == labelValue);

    // check frames
    auto callstack = sample.GetCallstack();
    ASSERT_EQ(3, callstack.size());
    int current = 1;
    for (auto frame : callstack)
    {
        ASSERT_EQ("module", frame.first);

        std::stringstream buffer;
        buffer << framePrefix << " #" << current;
        ASSERT_EQ(buffer.str(), frame.second);

        current++;
    }
}

// Add samples and check their presence
TEST(SamplesTimeProviderTest, CheckStore)
{
    TestSamplesProvider provider;

    std::string runtimeId = "MyRid";
    provider.Add(GetTestSample(runtimeId, "Frame", "thread name", "thread 1"));
    provider.Add(GetTestSample(runtimeId, "Frame", "thread name", "thread 2"));

    auto samples = provider.GetSamples();
    ASSERT_EQ(2, samples.size());

    size_t current = 1;
    for (auto const& sample : samples)
    {
        std::stringstream builder;
        builder << "thread " << current;
        ValidateTestSample(sample, "Frame", "thread name", builder.str());

        current++;
    }
}
