// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "EncodedPprof.h"
#include "PprofBuilder.h"
#include "ProfilerMockedInterface.h"
#include "Sample.h"

#include <algorithm>
#include <string>

using ::testing::Return;

namespace {
std::vector<SampleValueType> OneValueType()
{
    return std::vector<SampleValueType>({{"exception", "count"}});
}

// Returns true if 'needle' appears verbatim in the serialized bytes (the pprof
// string table stores strings as raw UTF-8, so frame/label text is searchable).
bool BytesContain(std::vector<uint8_t> const& bytes, std::string const& needle)
{
    if (needle.empty() || needle.size() > bytes.size())
    {
        return false;
    }
    return std::search(bytes.begin(), bytes.end(), needle.begin(), needle.end()) != bytes.end();
}
} // namespace

TEST(PprofBuilderTest, CreateReturnsNullptrWhenNoValueTypes)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), {}, "RealTime", "Nanoseconds", "my app");
    ASSERT_EQ(builder, nullptr);
}

TEST(PprofBuilderTest, CheckApplicationName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), OneValueType(), "RealTime", "Nanoseconds", "my app");
    ASSERT_NE(builder, nullptr);
    ASSERT_EQ("my app", builder->GetApplicationName());
}

TEST(PprofBuilderTest, AddSampleSucceeds)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), OneValueType(), "RealTime", "Nanoseconds", "my app");
    ASSERT_NE(builder, nullptr);

    Sample::ValuesCount = 1;
    auto sample = std::make_shared<Sample>(1ns, "rid", 2);
    sample->AddFrame({"myModule", "myFrame", "myFile.cs", 42});
    sample->AddValue(21, 0);

    auto success = builder->Add(sample);
    ASSERT_TRUE(success) << success.message();
}

TEST(PprofBuilderTest, SerializeProducesValidPprofWithFrameStrings)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), OneValueType(), "RealTime", "Nanoseconds", "my app");
    ASSERT_NE(builder, nullptr);

    Sample::ValuesCount = 1;
    auto sample = std::make_shared<Sample>(1ns, "rid", 2);
    sample->AddFrame({"myModule", "myFrame", "myFile.cs", 42});
    sample->AddValue(21, 0);
    sample->AddLabel(StringLabel{"my label", "my value"});
    ASSERT_TRUE(builder->Add(sample));

    auto encoded = builder->Serialize();

    ASSERT_FALSE(encoded.Bytes.empty());
    // sample-type + period strings
    ASSERT_TRUE(BytesContain(encoded.Bytes, "exception"));
    ASSERT_TRUE(BytesContain(encoded.Bytes, "RealTime"));
    // frame strings
    ASSERT_TRUE(BytesContain(encoded.Bytes, "myFrame"));
    ASSERT_TRUE(BytesContain(encoded.Bytes, "myModule"));
    ASSERT_TRUE(BytesContain(encoded.Bytes, "myFile.cs"));
    // label strings
    ASSERT_TRUE(BytesContain(encoded.Bytes, "my label"));
    ASSERT_TRUE(BytesContain(encoded.Bytes, "my value"));
}

TEST(PprofBuilderTest, AggregatesSamplesWithIdenticalStacksAndLabels)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), OneValueType(), "RealTime", "Nanoseconds", "my app");
    ASSERT_NE(builder, nullptr);

    Sample::ValuesCount = 1;

    for (int i = 0; i < 3; ++i)
    {
        auto sample = std::make_shared<Sample>(0ns, "rid", 1);
        sample->AddFrame({"m", "f", "file", 1});
        sample->AddValue(10, 0);
        ASSERT_TRUE(builder->Add(sample));
    }

    auto encoded = builder->Serialize();
    ASSERT_FALSE(encoded.Bytes.empty());
    // Aggregated to a single sample: value 30 (0x1e) must appear in the sample's packed values.
    ASSERT_TRUE(BytesContain(encoded.Bytes, std::string(1, static_cast<char>(0x1e))));
}

TEST(PprofBuilderTest, EndpointLabelIsAddedForMappedSpan)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    EXPECT_CALL(mockConfiguration, IsTimestampsAsLabelEnabled()).WillRepeatedly(Return(false));

    auto builder = PprofBuilder::Create(configuration.get(), OneValueType(), "RealTime", "Nanoseconds", "my app");
    ASSERT_NE(builder, nullptr);

    Sample::ValuesCount = 1;
    auto sample = std::make_shared<Sample>(0ns, "rid", 1);
    sample->AddFrame({"m", "f", "file", 1});
    sample->AddValue(1, 0);
    sample->AddLabel(NumericLabel{Sample::LocalRootSpanIdLabel, 1234});
    ASSERT_TRUE(builder->Add(sample));

    builder->SetEndpoint(1234, "GET /orders");
    builder->AddEndpointCount("GET /orders", 1);

    auto encoded = builder->Serialize();
    ASSERT_TRUE(BytesContain(encoded.Bytes, "trace endpoint"));
    ASSERT_TRUE(BytesContain(encoded.Bytes, "GET /orders"));
    ASSERT_EQ(encoded.EndpointCounts.size(), 1u);
    ASSERT_EQ(encoded.EndpointCounts["GET /orders"], 1);
}
