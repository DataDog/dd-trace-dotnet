// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <sstream>
#include <thread>

#include "Sample.h"
#include "SamplesProvider.h"


class TestSamplesProvider : public SamplesProvider
{
public:
    void Add(Sample&& sample)
    {
        Store(std::move(sample));
    }
};

Sample GetTestSample(const std::string& framePrefix, const std::string& labelId, const std::string& labelValue)
{
    Sample sample;
    // wall values
    sample.AddValue(100, SampleValue::WallTimeDuration);
    sample.AddValue(200, SampleValue::WallTimeDuration);
    sample.AddValue(300, SampleValue::WallTimeDuration);
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
    // today, only 1 value in the array
    // --> should be increased when a new profiler is added
    auto values = sample.GetValues();
    ASSERT_EQ(1, values.size());

    for (auto const& value : values)
    {
        // for the same SampleValue, only the last "added" value is kept
        ASSERT_EQ(300, value);
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

    provider.Add(GetTestSample("Frame", "thread name", "thread 1"));
    provider.Add(GetTestSample("Frame", "thread name", "thread 2"));

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
