// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gmock/gmock.h"
#include "gtest/gtest.h"

#include "CpuProfilerType.h"

TEST(CpuProfilerTypeTest, CheckEmptyStringConversion)
{
    CpuProfilerType result;
    ASSERT_FALSE(convert_to(WStr(""), result));
}

TEST(CpuProfilerTypeTest, CheckUnrecognizedType)
{
    CpuProfilerType result;
    ASSERT_FALSE(convert_to(WStr("UnknownType"), result));
}

TEST(CpuProfilerTypeTest, CheckManualCpuTime)
{
    CpuProfilerType result;
    ASSERT_TRUE(convert_to(WStr("ManualCpuTime"), result));

    ASSERT_EQ(CpuProfilerType::ManualCpuTime, result);
}

TEST(CpuProfilerTypeTest, CheckManualCpuTimeCaseInsensitive)
{
    CpuProfilerType result;
    ASSERT_TRUE(convert_to(WStr("maNualCPUTimE"), result));

    ASSERT_EQ(CpuProfilerType::ManualCpuTime, result);
}

#ifdef LINUX

TEST(CpuProfilerTypeTest, CheckTimerCreate)
{
    CpuProfilerType result;
    ASSERT_TRUE(convert_to(WStr("TimerCreate"), result));

    ASSERT_EQ(CpuProfilerType::TimerCreate, result);
}

TEST(CpuProfilerTypeTest, CheckTimerCreateInsensitive)
{
    CpuProfilerType result;
    ASSERT_TRUE(convert_to(WStr("TimERCreAte"), result));

    ASSERT_EQ(CpuProfilerType::TimerCreate, result);
}
#endif