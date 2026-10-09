// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "FfiHelper.h"

namespace libdatadog {

TEST(FfiHelperTest, ResolveCustomMemorySampleType)
{
    ddog_prof_SampleType sampleType;

    ASSERT_TRUE(TryCreateSampleType("memory-breakdown", "bytes", sampleType));
    EXPECT_EQ(DDOG_PROF_SAMPLE_TYPE_CUSTOM1, sampleType);
}

TEST(FfiHelperTest, RejectUnsupportedCustomMemorySampleTypes)
{
    ddog_prof_SampleType sampleType;

    EXPECT_FALSE(TryCreateSampleType("memory-breakdown", "byte", sampleType));
    EXPECT_FALSE(TryCreateSampleType("memory-breakdown", "count", sampleType));
    EXPECT_FALSE(TryCreateSampleType("committed", "bytes", sampleType));
    EXPECT_FALSE(TryCreateSampleType("rss", "bytes", sampleType));
}

TEST(FfiHelperTest, IdentifyCustomSampleTypes)
{
    EXPECT_TRUE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CUSTOM1));
    EXPECT_TRUE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CUSTOM2));
    EXPECT_TRUE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CUSTOM3));
    EXPECT_TRUE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CUSTOM4));
    EXPECT_TRUE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CUSTOM5));
    EXPECT_FALSE(IsCustomSampleType(DDOG_PROF_SAMPLE_TYPE_CPU_SAMPLES));
}

} // namespace libdatadog
