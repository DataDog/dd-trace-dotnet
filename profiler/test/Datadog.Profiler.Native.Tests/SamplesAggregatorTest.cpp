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

#include <chrono>
#include <tuple>

using ::testing::_;
using ::testing::ByMove;
using ::testing::Return;
using ::testing::Throw;

using namespace std::chrono_literals;

std::list<Sample> CreateSamples(int nbSamples)
{
    std::list<Sample> samples;
    for (int i = 0; i < nbSamples; i++)
    {
        samples.push_back({});
    }
    return samples;
}

TEST(SamplesAggregatorTest, MustCollectOneSampleFromOneProvider)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();


    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(1))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(1);
    EXPECT_CALL(mockExporter, Export()).Times(1);

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter);
    aggregator.Register(&mockSamplesProvider);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
}

TEST(SamplesAggregatorTest, MustCollectSamplesFromTwoProviders)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(1))));

    auto [samplesProvider2, mockSamplesProvider2] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider2, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(2))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(3);
    EXPECT_CALL(mockExporter, Export()).Times(1);

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter);
    aggregator.Register(&mockSamplesProvider);
    aggregator.Register(&mockSamplesProvider2);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
}

TEST(SamplesAggregatorTest, MustNotFailWhenSendingProfileThrows)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(1))));

    auto [samplesProvider2, mockSamplesProvider2] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider2, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(2))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(3);
    EXPECT_CALL(mockExporter, Export()).Times(1).WillRepeatedly(Throw(std::exception()));

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter);
    aggregator.Register(&mockSamplesProvider);
    aggregator.Register(&mockSamplesProvider2);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
}

TEST(SamplesAggregatorTest, MustNotFailWhenAddingSampleThrows)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));

    auto [samplesProvider, mockSamplesProvider] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(1))));

    auto [samplesProvider2, mockSamplesProvider2] = CreateSamplesProvider();
    EXPECT_CALL(mockSamplesProvider2, GetSamples()).Times(1).WillOnce(Return(ByMove(CreateSamples(2))));

    auto [exporter, mockExporter] = CreateExporter();
    EXPECT_CALL(mockExporter, Add(_)).Times(2).WillOnce(Return()).WillRepeatedly(Throw(std::exception()));
    EXPECT_CALL(mockExporter, Export()).Times(0);

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter);
    aggregator.Register(&mockSamplesProvider);
    aggregator.Register(&mockSamplesProvider2);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
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

    auto aggregator = SamplesAggregator(&mockConfiguration, &mockExporter);
    aggregator.Register(&mockSamplesProvider);

    aggregator.Start();
    std::this_thread::sleep_for(100ms);
    aggregator.Stop();
}
