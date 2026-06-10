// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Profile.h"
#include "ProfilerMockedInterface.h"

namespace libdatadog {

std::unique_ptr<Profile> CreateProfile(IConfiguration* configuration)
{
    auto p = Profile::Create(configuration, {{"cpu", "nanosecond"}}, "RealTime", "Nanoseconds", "my app");
    EXPECT_NE(p, nullptr);
    return p;
}

TEST(ProfileTest, CheckProfileName)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    ASSERT_EQ("my app", p->GetApplicationName());
}

TEST(ProfileTest, AddSample)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    Sample::ValuesCount = 1;
    auto s = std::make_shared<Sample>(1ns, "1", 2);
    s->AddFrame({"", "", "", 1});
    s->AddFrame({"", "", "", 2});
    s->AddValue(42, 0);
    auto success = p->Add(s);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, AddUpscalingRule)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    std::vector<SampleValueTypeProvider::Offset> offsets = {0};
    auto success = p->AddUpscalingRuleProportional(offsets, "my_label", "my_group", 2, 10);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, SetEndpoint)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    EXPECT_NO_THROW(p->SetEndpoint(42, "my_endpoint"));
}

TEST(ProfileTest, AddEndpointCount)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    EXPECT_NO_THROW(p->AddEndpointCount("my_endpoint", 1));
}

TEST(ProfileTest, EnsureAddFailIfWrongNumberOfValues)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    // change number of values to fail the Add
    Sample::ValuesCount = 2;
    auto s = std::make_shared<Sample>(1ns, "1", 2);
    s->AddFrame({"", "", "", 1});
    s->AddFrame({"", "", "", 2});
    s->AddValue(42, 0);
    auto success = p->Add(s);
    ASSERT_FALSE(success);
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(ProfileTest, EnsureAddUpscalingRuleFailIfMissingFields)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = CreateProfile(&configuration);
    ASSERT_NE(p, nullptr);

    std::vector<SampleValueTypeProvider::Offset> offsets;
    auto success = p->AddUpscalingRuleProportional(offsets, "", "", 0, 0);
    ASSERT_FALSE(success) << success.message();
}

TEST(ProfileTest, CreateProfileReturnsNullOnEmptyValueTypes)
{
    testing::NiceMock<MockConfiguration> configuration;
    auto p = Profile::Create(&configuration, {}, "RealTime", "Nanoseconds", "my app");
    ASSERT_EQ(p, nullptr);
}

} // namespace libdatadog