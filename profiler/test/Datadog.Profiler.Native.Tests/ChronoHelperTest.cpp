// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include <chrono>
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Windows/chrono_helper.hpp"

using namespace std::chrono_literals;

TEST(ChronoHelper, First)
{
    ASSERT_EQ(etw_timestamp::zero().count(), 0);

    auto v = 99ns;
    auto result = std::chrono::duration_cast<etw_timestamp>(v);
    auto expectedZero = etw_timestamp(0);

    ASSERT_EQ(result, expectedZero);

    auto w = 100ns;
    auto result2 = std::chrono::duration_cast<etw_timestamp>(w);
    auto expectedOne = etw_timestamp(1);
    ASSERT_EQ(result2, expectedOne);

    auto z = 1us;
    auto result3 = std::chrono::duration_cast<etw_timestamp>(z);
    auto expectedTen = etw_timestamp(10);
    ASSERT_EQ(result3, expectedTen);
}