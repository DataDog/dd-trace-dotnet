// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "FixedSizeAllocator.h"

#include "gtest/gtest.h"

TEST(FixedSizeAllocatorTest, OnlyOneAllocationCanBeDone)
{
    auto p = FixedSizeAllocator(1, 1);

    auto s1 = p.allocate(1);

    ASSERT_NE(s1, nullptr);

    auto s2 = p.allocate(1);

    ASSERT_EQ(s2, nullptr);
}

TEST(FixedSizeAllocatorTest, OnlyTwoAllocationsCanBeDone)
{
    auto p = FixedSizeAllocator(1, 2);

    // First callstack
    auto s1 = p.allocate(1);

    ASSERT_NE(s1, nullptr);

    // Second callstack
    auto s2 = p.allocate(1);

    ASSERT_NE(s1, nullptr);

    // No more available callstack
    // unusable callstack is returned
    auto s3 = p.allocate(1);

    ASSERT_EQ(s3, nullptr);
}

TEST(FixedSizeAllocatorTest, MakeSureAllocatedByteCanBeReused)
{
    auto p = FixedSizeAllocator(1, 1);

    auto s1 = p.allocate(1);
    ASSERT_NE(s1, nullptr);
    p.deallocate(s1, 1);

    auto s2 = p.allocate(1);
    ASSERT_NE(s2, nullptr);
}

TEST(FixedSizeAllocatorTest, MustThrowIfAllocationSizeIsBiggerThanBlockSize)
{
    auto p = FixedSizeAllocator(1, 1);

    ASSERT_THROW(auto v = p.allocate(2), std::bad_alloc);
}

TEST(FixedSizeAllocatorTest, MakeSureCorrectlyAligned)
{
    auto p = FixedSizeAllocator(1, 3);

    auto s1 = p.allocate(1);
    ASSERT_EQ((std::uintptr_t)s1 % alignof(void*), 0);

    auto s2 = p.allocate(1);
    ASSERT_EQ((std::uintptr_t)s2 % alignof(void*), 0);

    auto s3 = p.allocate(1);
    ASSERT_EQ((std::uintptr_t)s3 % alignof(void*), 0);
}
