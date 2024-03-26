// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

#include <fstream>

#include "gtest/gtest.h"

TEST(CallstackPoolTest, OnlyOneCallstackCanBeAcquired)
{
    auto p = CallstackPool(1);

    auto s1 = p.Get();

    ASSERT_EQ(s1.Capacity(), Callstack::MaxFrames);

    auto s2 = p.Get();

    ASSERT_EQ(s2.Capacity(), 0);
}

TEST(CallstackPoolTest, MoreThanOneCallstackCanBeAcquired)
{
    auto p = CallstackPool(2);

    // First callstack
    auto s1 = p.Get();

    ASSERT_EQ(s1.Capacity(), Callstack::MaxFrames);

    // Second callstack
    auto s2 = p.Get();

    ASSERT_EQ(s2.Capacity(), Callstack::MaxFrames);

    // No more available callstack
    // unusable callstack is returned
    auto s3 = p.Get();

    ASSERT_EQ(s3.Capacity(), 0);
}

TEST(CallstackPoolTest, MakeSureCallstackIsReleasedAndReused)
{
    auto p = CallstackPool(1);

    {
        auto s1 = p.Get();
        ASSERT_EQ(s1.Capacity(), Callstack::MaxFrames);
    }

    auto s2 = p.Get();

    ASSERT_EQ(s2.Capacity(), Callstack::MaxFrames);
}
