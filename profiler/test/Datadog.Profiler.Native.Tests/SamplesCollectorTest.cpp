// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SamplesCollector.h"

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Configuration.h"
#include "IExporter.h"
#include "ISamplesProvider.h"
#include "ProfilerMockedInterface.h"
#include "Sample.h"
#include "SamplesAggregator.h"
#include "ThreadsCpuManagerHelper.h"

#include <chrono>
#include <list>
#include <tuple>

using ::testing::_;
using ::testing::ByMove;
using ::testing::Invoke;
using ::testing::InvokeWithoutArgs;
using ::testing::Return;
using ::testing::Throw;

using namespace std::chrono_literals;

class FakeSamplesProvider : public ISamplesProvider
{
public:
    FakeSamplesProvider(std::string_view runtimeId, int nbSamples) :
        _calls{0},
        _runtimeId{runtimeId},
        _nbSamples{nbSamples}
    {
    }

    std::list<Sample> GetSamples() override
    {
        _calls++;
        return std::move(CreateSamples(_runtimeId, _nbSamples));
    }

    int GetNbCalls()
    {
        return _calls;
    }

    Sample CreateSample(std::string_view rid)
    {
        Sample s{rid};

        s.AddFrame("My module", "My frame");

        return s;
    }

    std::list<Sample> CreateSamples(std::string_view runtimeId, int nbSamples)
    {
        std::list<Sample> samples;
        for (int i = 0; i < nbSamples; i++)
        {
            samples.push_back(CreateSample(runtimeId));
        }
        return samples;
    }

private:
    std::string_view _runtimeId;
    int _nbSamples;
    int _calls;
};

TEST(SamplesCollectorTest, MustCollectSamplesFromTwoProviders)
{
    std::string runtimeId = "MyRid";
    FakeSamplesProvider samplesProvider(runtimeId, 1);

    std::string runtimeId2 = "MyRid2";
    FakeSamplesProvider samplesProvider2(runtimeId2, 2);

    auto threadsCpuManagerHelper = ThreadsCpuManagerHelper();

    auto collector = SamplesCollector(&threadsCpuManagerHelper);
    collector.Register(&samplesProvider);
    collector.Register(&samplesProvider2);

    collector.Start();
    // wait for more than upload interval so that ProcessSamples() is called multiple times
    std::this_thread::sleep_for(90ms);
    
    collector.Stop();

    auto exportsCount = samplesProvider.GetNbCalls();
    ASSERT_GE(exportsCount, 2);
    auto exportsCount2 = samplesProvider2.GetNbCalls();
    ASSERT_GE(exportsCount2, 2);

    auto samples = collector.GetSamples();

    uint32_t samplesCount1 = 0;
    uint32_t samplesCount2 = 0;

    for (auto& sample : samples)
    {
        if (sample.GetRuntimeId() == runtimeId)
        {
            samplesCount1++;
        }
        else if (sample.GetRuntimeId() == runtimeId2)
        {
            samplesCount2++;
        }
        else
        {
            // unexpected
            ASSERT_TRUE(false);
        }
    }

    ASSERT_EQ(samplesCount1, exportsCount);
    ASSERT_EQ(samplesCount2, exportsCount2 * 2);

    auto samples2 = collector.GetSamples();

    ASSERT_EQ(samples2.size(), 0);
}

TEST(SamplesCollectorTest, MustStopCollectingSamples)
{
    std::string runtimeId = "MyRid";
    FakeSamplesProvider samplesProvider(runtimeId, 1);

    auto threadsCpuManagerHelper = ThreadsCpuManagerHelper();

    auto collector = SamplesCollector(&threadsCpuManagerHelper);
    collector.Register(&samplesProvider);

    collector.Start();
    // wait for more than upload interval so that ProcessSamples() is called multiple times
    std::this_thread::sleep_for(90ms);
    
    collector.Stop();

    auto exportsCount = samplesProvider.GetNbCalls();

    ASSERT_GE(exportsCount, 2);

    std::this_thread::sleep_for(200ms);

    auto newExportsCount = samplesProvider.GetNbCalls();

    ASSERT_EQ(newExportsCount, exportsCount);
}