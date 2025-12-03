// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "SampleValueTypeProvider.h"

#include "gmock/gmock.h"
#include "gtest/gtest.h"

static SampleValueType CpuValueType = {"cpu", "nanoseconds", -1};
static SampleValueType WallTimeValueType = {"walltime", "nanoseconds", -1};
static SampleValueType ExceptionValueType = {"exception", "count", -1};

testing::AssertionResult AreSampleValueTypeEqual(SampleValueType const& v1, SampleValueType const& v2)
{
    if (v1.Name == v2.Name && v1.Unit == v2.Unit)
        return testing::AssertionSuccess();
    else
        return testing::AssertionFailure() << " sample value type differs";
}

#define ASSERT_DEFINITIONS(expectedDefinitions, definitions)   \
    ASSERT_EQ(expectedDefinitions.size(), definitions.size()); \
    for (std::size_t i = 0; i < expectedDefinitions.size(); i++)      \
        ASSERT_PRED2(AreSampleValueTypeEqual, expectedDefinitions[i], definitions[i]);

#define ASSERT_OFFSETS(expectedOffsets, offsets)       \
    ASSERT_EQ(expectedOffsets.size(), offsets.size()); \
    for (std::size_t i = 0; i < expectedOffsets.size(); i++)  \
        ASSERT_EQ(expectedOffsets[i], offsets[i]);

using ValueOffsets = std::vector<SampleValueTypeProvider::Offset>;

TEST(SampleValueTypeProvider, RegisterValueTypes)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.GetOrRegister(valueTypes);

    ASSERT_OFFSETS((ValueOffsets{0, 1}), offsets);
    //              cpu offset --^  ^-- walltime offset

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, RegisterTwiceSameValueType)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.GetOrRegister(valueTypes);

    ASSERT_OFFSETS((ValueOffsets{0, 1}), offsets);
    //              cpu offset --^  ^-- walltime offset

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register walltime a second time

    auto alreadyRegisteredValueType = std::vector<SampleValueType>{WallTimeValueType};
    auto offsets2 = provider.GetOrRegister(alreadyRegisteredValueType);

    ASSERT_OFFSETS((ValueOffsets{1}), offsets2); // walltime offset did not changed

    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, CheckSequentialRegistration)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType, WallTimeValueType};

    auto offsets = provider.GetOrRegister(valueTypes);
    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register ExceptionValueType
    auto anotherValuetype = std::vector<SampleValueType>{ExceptionValueType};

    auto offsets2 = provider.GetOrRegister(anotherValuetype);
    ASSERT_OFFSETS((ValueOffsets{2}), offsets2);

    ASSERT_DEFINITIONS((std::vector<SampleValueType>{CpuValueType, WallTimeValueType, ExceptionValueType}), provider.GetValueTypes());
}

TEST(SampleValueTypeProvider, EnsureThrowIfAddValueTypeSameNameButDifferentUnit)
{
    SampleValueTypeProvider provider;
    auto valueTypes = std::vector<SampleValueType>{CpuValueType};

    auto offsets = provider.GetOrRegister(valueTypes);
    ASSERT_DEFINITIONS(valueTypes, provider.GetValueTypes());

    // Register a cpu value but with different unit
    auto anotherValuetype = std::vector<SampleValueType>{{"cpu", "non-sense-unit"}};

    EXPECT_THROW(provider.GetOrRegister(anotherValuetype), std::runtime_error);
}

TEST(SampleValueTypeProvider, CheckSequentialIndex)
{
    std::vector<SampleValueType> AllocationSampleTypeDefinitions(
        {{"alloc-samples", "count", -1},
         {"alloc-size", "bytes", -1}});
    std::vector<SampleValueType> ExceptionSampleTypeDefinitions(
        {{"exception", "count", -1}});
    std::vector<SampleValueType> CpuSampleTypeDefinitions(
        {{"cpu", "nanoseconds", -1},
         {"cpu-samples", "count", -1}});

    SampleValueTypeProvider provider;
    auto allocationsOffsets = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    auto exceptionOffsets = provider.GetOrRegister(ExceptionSampleTypeDefinitions);
    auto cpuOffsets = provider.GetOrRegister(CpuSampleTypeDefinitions);

    ASSERT_EQ(AllocationSampleTypeDefinitions[0].Index, 0);
    ASSERT_EQ(AllocationSampleTypeDefinitions[1].Index, 0);
    ASSERT_EQ(ExceptionSampleTypeDefinitions[0].Index, 1);
    ASSERT_EQ(CpuSampleTypeDefinitions[0].Index, 2);
    ASSERT_EQ(CpuSampleTypeDefinitions[1].Index, 2);
}

TEST(SampleValueTypeProvider, CheckReusedSampleValueType)
{
    std::vector<SampleValueType> AllocationSampleTypeDefinitions(
        {{"alloc-samples", "count", -1},
         {"alloc-size", "bytes", -1}});

    SampleValueTypeProvider provider;

    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);

    ASSERT_EQ(AllocationSampleTypeDefinitions[0].Index, 0);
    ASSERT_EQ(AllocationSampleTypeDefinitions[1].Index, 0);
}

TEST(SampleValueTypeProvider, CheckSequentialIndexWithReusedSampleValueType)
{
    std::vector<SampleValueType> AllocationSampleTypeDefinitions(
        {{"alloc-samples", "count", -1},
         {"alloc-size", "bytes", -1}});
    std::vector<SampleValueType> ExceptionSampleTypeDefinitions(
        {{"exception", "count", -1}});
    std::vector<SampleValueType> CpuSampleTypeDefinitions(
        {{"cpu", "nanoseconds", -1},
        {"cpu-samples", "count", -1}});

    SampleValueTypeProvider provider;
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(ExceptionSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(AllocationSampleTypeDefinitions);
    std::ignore = provider.GetOrRegister(CpuSampleTypeDefinitions);


    ASSERT_EQ(AllocationSampleTypeDefinitions[0].Index, 0);
    ASSERT_EQ(AllocationSampleTypeDefinitions[1].Index, 0);
    ASSERT_EQ(ExceptionSampleTypeDefinitions[0].Index, 1);
    ASSERT_EQ(CpuSampleTypeDefinitions[0].Index, 2);
    ASSERT_EQ(CpuSampleTypeDefinitions[1].Index, 2);
}