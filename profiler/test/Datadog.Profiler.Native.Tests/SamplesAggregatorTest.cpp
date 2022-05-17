// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "Configuration.h"
#include "IExporter.h"
#include "ISamplesProvider.h"
#include "ProfilerMockedInterface.h"
#include "Sample.h"
#include "SamplesAggregator.h"

#include <list>
#include <chrono>
#include <tuple>

using ::testing::_;
using ::testing::ByMove;
using ::testing::Return;
using ::testing::Throw;

using namespace std::chrono_literals;

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

class FakeSamplesProvider : public ISamplesProvider
{
public:
    FakeSamplesProvider(std::string_view runtimeId, int nbSamples)
        :
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

private:
    std::string_view _runtimeId;
    int _nbSamples;
    int _calls;
};


TEST(SamplesAggregatorTest, MustCollectOneSampleFromOneProvider)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    std::string runtimeId = "MyRid";
    FakeSamplesProvider samplesProvider(runtimeId, 1);

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(1*2); // 1 sample returned twice
    EXPECT_CALL(mockExporter, Export()).Times(2).WillRepeatedly(Return(true));

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);
    aggregator.Register(&samplesProvider);

    aggregator.Start();

    // wait for more than upload interval so that ProcessSamples()
    // runs once before Stop()
    std::this_thread::sleep_for(1500ms);
    auto exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 1);

    aggregator.Stop();
    exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 2);  // the last .pprof triggers one more call

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustCollectSamplesFromTwoProviders)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    std::string runtimeId = "MyRid";
    FakeSamplesProvider samplesProvider(runtimeId, 1);

    std::string runtimeId2 = "MyRid2";
    FakeSamplesProvider samplesProvider2(runtimeId2, 2);

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(3*2);  // 3 samples returned twice
    EXPECT_CALL(mockExporter, Export()).Times(2).WillRepeatedly(Return(true));

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);
    aggregator.Register(&samplesProvider);
    aggregator.Register(&samplesProvider2);

    aggregator.Start();
    // wait for more than upload interval so that ProcessSamples()
    // runs once before Stop()
    std::this_thread::sleep_for(1500ms);
    auto exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 1);
    auto exportsCount2 = samplesProvider2.GetNbCalls();
    ASSERT_EQ(exportsCount2, 1);

    aggregator.Stop();
    // the last .pprof triggers one more call on each provider
    exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 2);
    exportsCount2 = samplesProvider2.GetNbCalls();
    ASSERT_EQ(exportsCount, 2);

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustExportAfterStop)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(2s));

    std::string runtimeId = "MyRid";
    FakeSamplesProvider samplesProvider(runtimeId, 1);

    auto [exporter, mockExporter] = CreateExporter();

    // the provider and exporter are supposed to be called once AFTER Stop()
    EXPECT_CALL(mockExporter, Add(_)).Times(1);
    EXPECT_CALL(mockExporter, Export()).Times(1).WillRepeatedly(Return(true));

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);
    aggregator.Register(&samplesProvider);

    aggregator.Start();

    // wait less than upload interval to ensure that ProcessSamples()
    // won't be called
    std::this_thread::sleep_for(200ms);
    auto exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 0);

    aggregator.Stop();
    // Stop() is supposed to flush the remaining samples
    // from the provider and export them to a final and unique .pprof
    exportsCount = samplesProvider.GetNbCalls();
    ASSERT_EQ(exportsCount, 1);

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustNotFailWhenSendingProfileThrows)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    std::string runtimeId = "MyRid";
    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(runtimeId, 1))));

    std::string runtimeId2 = "MyRid2";
    auto [samplesProvider2, mockSamplesProvider2] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider2, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(runtimeId2, 2))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(3);
    EXPECT_CALL(mockExporter, Export()).Times(1).WillRepeatedly(Throw(std::exception()));

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);

    aggregator.Register(&mockSamplesProvider);
    aggregator.Register(&mockSamplesProvider2);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustNotFailWhenAddingSampleThrows)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    std::string runtimeId = "MyRid";
    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(runtimeId, 1))));

    std::string runtimeId2 = "MyRid2";
    auto [samplesProvider2, mockSamplesProvider2] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider2, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(runtimeId2, 2))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(2).WillOnce(Return()).WillRepeatedly(Throw(std::exception()));
    EXPECT_CALL(mockExporter, Export()).Times(0);

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);

    aggregator.Register(&mockSamplesProvider);
    aggregator.Register(&mockSamplesProvider2);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustNotFailWhenCollectingSampleThrows)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Throw(std::exception()));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(0);
    EXPECT_CALL(mockExporter, Export()).Times(0);

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);
    aggregator.Register(&mockSamplesProvider);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();

    ASSERT_TRUE(metricsSender.WasCounterCalled());
}

TEST(SamplesAggregatorTest, MustdNotAddSampleInExporterIfEmptyCallstack)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(10s));

    std::string runtimeId = "MyRid";

    std::list<Sample> samples;
    // add sample with empty callstack
    samples.push_back({runtimeId});

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(std::move(samples))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(0);

    auto metricsSender = MockMetricsSender();

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter, &metricsSender);
    aggregator.Register(&mockSamplesProvider);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
}