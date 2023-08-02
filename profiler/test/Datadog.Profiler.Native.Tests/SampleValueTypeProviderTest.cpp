// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SampleValueTypeProvider.h"

#include "gmock/gmock.h"
#include "gtest/gtest.h"

static const SampleValueType CpuValueType = {"cpu", "nanoseconds"};
static const SampleValueType WallTimeValueType = {"walltime", "nanoseconds"};
static const SampleValueType ExceptionValueType = {"exception", "count"};

testing::AssertionResult AreSampleValueTypeEqual(SampleValueType const& v1, SampleValueType const& v2)
{
    if (v1.Name == v2.Name && v1.Unit == v2.Unit)
        return testing::AssertionSuccess();
    else
        return testing::AssertionFailure() << " sample value type differs";
}

#define ASSERT_DEFINITIONS(expectedDefinitions, definitions)   \
    ASSERT_EQ(expectedDefinitions.size(), definitions.size()); \
    for (auto i = 0; i < expectedDefinitions.size(); i++)      \
        ASSERT_PRED2(AreSampleValueTypeEqual, expectedDefinitions[i], definitions[i]);

TEST(SampleValueTypeProvider, RegisterValueTypes)
{
    SampleValueTypeProvider provider;
    auto const valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.Register(valueTypes);

    ASSERT_EQ(2, offsets.size());
    ASSERT_EQ(0, offsets[0]); // cpu offset
    ASSERT_EQ(1, offsets[1]); // walltime offset

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, RegisterTwiceSameValueType)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.Register(valueTypes);

    ASSERT_EQ(2, offsets.size());
    ASSERT_EQ(0, offsets[0]); // cpu offset
    ASSERT_EQ(1, offsets[1]); // walltime offset

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register a second time

    auto alreadyRegisteredValueType = std::vector<SampleValueType>{WallTimeValueType};
    auto offsets2 = provider.Register(alreadyRegisteredValueType);

    ASSERT_EQ(1, offsets2.size());
    ASSERT_EQ(1, offsets2[0]); // walltime offset did not changed

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, CheckSequentialRegistration)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.Register(valueTypes);
    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register ExceptionValueType
    auto anotherValuetype = std::vector<SampleValueType>{
        ExceptionValueType};

    auto offsets2 = provider.Register(anotherValuetype);

    ASSERT_EQ(1, offsets2.size());
    ASSERT_EQ(2, offsets2[0]);

    ASSERT_DEFINITIONS((std::vector<SampleValueType>{CpuValueType, WallTimeValueType, ExceptionValueType}), provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, EnsureThrowIfAddValueTypeSameNameButDifferentUnit)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType};

    auto offsets = provider.Register(valueTypes);
    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register a cpu value but with different unit
    auto anotherValuetype = std::vector<SampleValueType>{{"cpu", "non-sense-unit"}};

    EXPECT_THROW(provider.Register(anotherValuetype), std::runtime_error);
}