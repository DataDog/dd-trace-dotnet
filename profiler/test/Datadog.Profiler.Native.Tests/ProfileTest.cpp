// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Profile.h"
#include "ProfilerMockedInterface.h"

namespace libdatadog {

std::unique_ptr<Profile> CreateProfile(std::unique_ptr<IConfiguration> const& configuration)
{
    auto p = Profile::Create(configuration.get(), {{"cpu", "nanosecond"}}, "RealTime", "Nanoseconds", "my app");
    EXPECT_NE(p, nullptr);
    return p;
}

TEST(ProfileTest, CheckProfileName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
    ASSERT_NE(p, nullptr);

    ASSERT_EQ("my app", p->GetApplicationName());
}

TEST(ProfileTest, AddSample)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
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
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
    ASSERT_NE(p, nullptr);

    std::vector<SampleValueTypeProvider::Offset> offsets = {0};
    auto success = p->AddUpscalingRuleProportional(offsets, "my_label", "my_group", 2, 10);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, SetEndpoint)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
    ASSERT_NE(p, nullptr);

    EXPECT_NO_THROW(p->SetEndpoint(42, "my_endpoint"));
}

TEST(ProfileTest, AddEndpointCount)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
    ASSERT_NE(p, nullptr);

    EXPECT_NO_THROW(p->AddEndpointCount("my_endpoint", 1));
}

TEST(ProfileTest, EnsureAddFailIfWrongNumberOfValues)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
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
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = CreateProfile(configuration);
    ASSERT_NE(p, nullptr);

    std::vector<SampleValueTypeProvider::Offset> offsets;
    auto success = p->AddUpscalingRuleProportional(offsets, "", "", 0, 0);
    ASSERT_FALSE(success) << success.message();
}

TEST(ProfileTest, CreateProfileReturnsNullOnEmptyValueTypes)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    auto p = Profile::Create(configuration.get(), {}, "RealTime", "Nanoseconds", "my app");
    ASSERT_EQ(p, nullptr);
}

} // namespace libdatadog