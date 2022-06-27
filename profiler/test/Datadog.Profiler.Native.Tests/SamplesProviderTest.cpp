// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <sstream>
#include <thread>

#include "ProviderBase.h"
#include "Sample.h"

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
    // exception values
    sample.AddValue(4, SampleValue::ExceptionCount);
    sample.AddValue(5, SampleValue::ExceptionCount);
    sample.AddValue(6, SampleValue::ExceptionCount);
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
    // Today, only 3 values in the array
    //    WallTime
    //    CpuTime
    //    ExceptionCount
    // --> should be increased when a new profiler is added
    //     this is a good reminder to add dedicated tests  :^)
    auto values = sample.GetValues();
    ASSERT_EQ(3, values.size());

    for (size_t current = 0; current < 3; current++)
    {
        // for the same SampleValue, only the last "added" value is kept
        // update GetTestSample() for new profilers
        if (current == (size_t)SampleValue::WallTimeDuration)
        {
            ASSERT_EQ(300, values[current]);
        }
        else if (current == (size_t)SampleValue::CpuTimeDuration)
        {
            ASSERT_EQ(100, values[current]);
        }
        else if (current == (size_t)SampleValue::ExceptionCount)
        {
            ASSERT_EQ(6, values[current]);
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
