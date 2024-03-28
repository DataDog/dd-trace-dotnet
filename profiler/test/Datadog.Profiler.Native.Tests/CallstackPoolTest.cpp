// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"

#include "FixedSizeAllocator.h"

#include "gtest/gtest.h"

TEST(CallstackPoolTest, OnlyOneCallstackCanBeAcquiredWithFixedSizeAllocator)
{
    auto allocator = FixedSizeAllocator(Callstack::MaxSize, 1);
    auto p = CallstackPool(&allocator);

    auto s1 = p.Get();

    ASSERT_EQ(s1.Capacity(), Callstack::MaxFrames);

    auto s2 = p.Get();

    ASSERT_EQ(s2.Capacity(), 0);
}


TEST(CallstackPoolTest, MakeSureWeCanAllocateAsWeWantWithDefaultAllocator)
{
    auto p = CallstackPool(pmr::get_default_resource());

    auto s1 = p.Get();

    ASSERT_EQ(s1.Capacity(), Callstack::MaxFrames);

    auto s2 = p.Get();

    ASSERT_EQ(s2.Capacity(), Callstack::MaxFrames);

    auto s3 = p.Get();

    ASSERT_EQ(s3.Capacity(), Callstack::MaxFrames);
}

TEST(CallstackPoolTest, MakeSureCanUseAFullBigBuffer)
{
    constexpr std::size_t nbCallstacks = 10000;
    FixedSizeAllocator allocator(Callstack::MaxSize, nbCallstacks);

    auto p = CallstackPool(&allocator);

    std::vector<Callstack> v;
    v.reserve(600);

    for (auto i = 0; i < nbCallstacks; i++)
    {
        v.push_back(p.Get());

        auto& c = v.back();
        ASSERT_EQ(c.Capacity(), Callstack::MaxFrames) << "Failed at " << i;
    }


    // Check that we cannot insert
    auto c = p.Get();
    ASSERT_EQ(c.Capacity(), 0);
}