//// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
//// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
//
//#include "gmock/gmock.h"
//#include "gtest/gtest.h"
//
//#include "Configuration.h"
//#include "IExporter.h"
//#include "ISamplesProvider.h"
//#include "ProfilerMockedInterface.h"
//#include "Sample.h"
//#include "ThreadsCpuManagerHelper.h"
//
//#include <chrono>
//#include <list>
//#include <tuple>
//
//using ::testing::_;
//using ::testing::ByMove;
//using ::testing::InvokeWithoutArgs;
//using ::testing::Return;
//using ::testing::Throw;
//
//using namespace std::chrono_literals;
//
//Sample CreateSample(std::string_view rid)
//{
//    Sample s{rid};
//
//    s.AddFrame("My module", "My frame");
//
//    return s;
//}
//
//std::list<Sample> CreateSamples(std::string_view runtimeId, int nbSamples)
//{
//    std::list<Sample> samples;
//    for (int i = 0; i < nbSamples; i++)
//    {
//        samples.push_back(CreateSample(runtimeId));
//    }
//    return samples;
//}
//
//TEST(SamplesAggregatorTest, MustCollectSamples)
//{
//    auto [configuration, mockConfiguration] = CreateConfiguration();
//    EXPECT_CALL(mockConfiguration, GetUploadInterval()).Times(1).WillOnce(Return(1s));
//
//    auto [exporter, mockExporter] = CreateExporter();
//    EXPECT_CALL(mockExporter, Add(_)).Times(3 * 2); // 3 samples returned twice
//    EXPECT_CALL(mockExporter, Export()).Times(2).WillRepeatedly(Return(true));
//
//    auto metricsSender = MockMetricsSender();
//    auto threadsCpuManagerHelper = ThreadsCpuManagerHelper();
//
//    auto [collector, mockCollector] = CreateSamplesCollector();
//
//    std::string runtimeId = "MyRid";
//
//    uint32_t getSamplesCallCounter = 0;
//
//    EXPECT_CALL(mockCollector, GetSamples())
//        .WillRepeatedly(InvokeWithoutArgs([&getSamplesCallCounter, runtimeId] {
//            getSamplesCallCounter++;
//            return CreateSamples(runtimeId, 3);
//        }));
//
//    auto aggregator = SamplesAggregator(&mockConfiguration, &threadsCpuManagerHelper, &mockExporter, &metricsSender, collector.get());
//
//    aggregator.Start();
//    // wait for more than upload interval so that ProcessSamples()
//    // runs once before Stop()
//    std::this_thread::sleep_for(1500ms);
//
//    ASSERT_EQ(getSamplesCallCounter, 1);
//
//    aggregator.Stop();
//    // the last .pprof triggers one more call
//    ASSERT_EQ(getSamplesCallCounter, 2);
//
//    ASSERT_TRUE(metricsSender.WasCounterCalled());
//}
//
