// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Profile.h"

namespace libdatadog {

Profile CreateProfile()
{
    return Profile({{"cpu", "nanosecond"}}, "RealTime", "Nanoseconds", "my app");
}

TEST(ProfileTest, CheckProfileName)
{
    auto p = CreateProfile();

    ASSERT_EQ("my app", p.GetApplicationName());
}

TEST(ProfileTest, AddSample)
{
    auto p = CreateProfile();

    Sample::ValuesCount = 1;
    auto s = std::make_shared<Sample>(1, "1", 2);
    s->AddFrame({"", "", "", 1});
    s->AddFrame({"", "", "", 2});
    s->AddValue(42, 0);
    auto success = p.Add(s);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, AddUpscalingRule)
{
    auto p = CreateProfile();

    std::vector<SampleValueTypeProvider::Offset> offsets = {0};
    auto success = p.AddUpscalingRuleProportional(offsets, "my_label", "my_group", 2, 10);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, SetEndpoint)
{
    auto p = CreateProfile();

    EXPECT_NO_THROW(p.SetEndpoint(42, "my_endpoint"));
}

TEST(ProfileTest, AddEndpointCount)
{
    auto p = CreateProfile();

    EXPECT_NO_THROW(p.AddEndpointCount("my_endpoint", 1));
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(ProfileTest, EnsureAddFailIfWrongNumberOfValues)
{
    auto p = CreateProfile();

    // change number of values to fail the Add
    Sample::ValuesCount = 2;
    auto s = std::make_shared<Sample>(1, "1", 2);
    s->AddFrame({"", "", "", 1});
    s->AddFrame({"", "", "", 2});
    s->AddValue(42, 0);
    auto success = p.Add(s);
    ASSERT_FALSE(success);
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(ProfileTest, EnsureAddUpscalingRuleFailIfMissingFields)
{
    auto p = CreateProfile();
    std::vector<SampleValueTypeProvider::Offset> offsets;
    auto success = p.AddUpscalingRuleProportional(offsets, "", "", 0, 0);
    ASSERT_FALSE(success) << success.message();
}

} // namespace libdatadog