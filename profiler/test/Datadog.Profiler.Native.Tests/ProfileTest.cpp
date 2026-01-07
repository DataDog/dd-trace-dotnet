// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Profile.h"
#include "ProfilerMockedInterface.h"
#include "SymbolsStore.h"
#include "ServiceWrapper.hpp"

namespace libdatadog {

Profile CreateProfile(std::unique_ptr<IConfiguration> const& configuration, libdatadog::SymbolsStore* symbolsStore)
{
    return Profile(configuration.get(), {{"cpu", "nanosecond"}}, "RealTime", "Nanoseconds", "my app", symbolsStore);
}

TEST(ProfileTest, CheckProfileName)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    ASSERT_EQ("my app", p.GetApplicationName());
}

TEST(ProfileTest, AddSample)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    Sample::ValuesCount = 1;
    auto s = std::make_shared<Sample>(1ns, "1", 2, symbolsStore);
    auto emptyFunctionId = symbolsStore->InternFunction("", "");
    auto emptyModuleId = symbolsStore->InternMapping("");
    s->AddFrame({emptyModuleId.value(), emptyFunctionId.value(), 1});
    s->AddFrame({emptyModuleId.value(), emptyFunctionId.value(), 2});
    s->AddValue(42, 0);
    auto success = p.Add(s);
    ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, AddUpscalingRule)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    //auto success = p.AddUpscalingRuleProportional(0, symbolsStore->InternString("my_label").value(), "my_group", 2, 10);
    //ASSERT_TRUE(success) << success.message();
}

TEST(ProfileTest, SetEndpoint)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    EXPECT_NO_THROW(p.SetEndpoint(42, "my_endpoint"));
}

TEST(ProfileTest, AddEndpointCount)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    EXPECT_NO_THROW(p.AddEndpointCount("my_endpoint", 1));
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(ProfileTest, EnsureAddFailIfWrongNumberOfValues)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);

    // change number of values to fail the Add
    Sample::ValuesCount = 2;
    auto s = std::make_shared<Sample>(1ns, "1", 2, symbolsStore);
    auto emptyFunctionId = symbolsStore->InternFunction("", "");
    auto emptyModuleId = symbolsStore->InternMapping("");
    s->AddFrame({emptyModuleId.value(), emptyFunctionId.value(), 1});
    s->AddFrame({emptyModuleId.value(), emptyFunctionId.value(), 2});
    s->AddValue(42, 0);
    auto success = p.Add(s);

    ASSERT_FALSE(success);
}

// Test is mainly meant for memory leak detection (unit tests are run with ASAN)
TEST(ProfileTest, EnsureAddUpscalingRuleFailIfMissingFields)
{
    auto [configuration, mockConfiguration] = CreateConfiguration();
    ServiceWrapper<libdatadog::SymbolsStore> symbolsStore;
    auto p = CreateProfile(configuration, symbolsStore);
    //auto success = p.AddUpscalingRuleProportional(2, DDOG_PROF_STRINGID_EMPTY, "", 0, 0);
    //ASSERT_FALSE(success) << success.message();
}

} // namespace libdatadog