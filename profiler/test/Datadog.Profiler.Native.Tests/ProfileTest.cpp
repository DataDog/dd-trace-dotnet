// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "Profile.h"
#include "ProfilerMockedInterface.h"
#include "SymbolsStore.h"
#include "ServiceWrapper.hpp"

#define INTERN_MODULE(m)                                                \
    auto m##Id = symbolsStore->InternMapping(#m);                       \
    if (!m##Id)                                                         \
    {                                                                   \
        ASSERT_TRUE(false) << "Failed to intern module '" << #m << "'"; \
    }

#define INTERN_FUNCTION(fn)                                                 \
    auto fn##Id = symbolsStore->InternFunction(#fn, "");                    \
    if (!fn##Id)                                                            \
    {                                                                       \
        ASSERT_TRUE(false) << " Failed to intern function '" << #fn << "'"; \
    }

#define INTERN_STRING(s)                                                \
    auto s##Id = symbolsStore->InternString(#s);                        \
    if (!s##Id)                                                         \
    {                                                                   \
        ASSERT_TRUE(false) << "Failed to intern string '" << #s << "'"; \
    }

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

    INTERN_MODULE(emptyModule);
    INTERN_FUNCTION(emptyFunction);

    Sample::ValuesCount = 1;
    auto s = std::make_shared<Sample>(1ns, "1", 2, symbolsStore);
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

    std::vector<SampleValueTypeProvider::Offset> offsets = {0};
    auto success = p.AddUpscalingRuleProportional(offsets, "my_label", "my_group", 2, 10);
    ASSERT_TRUE(success) << success.message();
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

    INTERN_MODULE(emptyModule);
    INTERN_FUNCTION(emptyFunction);

    // change number of values to fail the Add
    Sample::ValuesCount = 2;
    auto s = std::make_shared<Sample>(1ns, "1", 2, symbolsStore);
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
    std::vector<SampleValueTypeProvider::Offset> offsets;
    auto success = p.AddUpscalingRuleProportional(offsets, "", "", 0, 0);
    ASSERT_FALSE(success) << success.message();
}

} // namespace libdatadog