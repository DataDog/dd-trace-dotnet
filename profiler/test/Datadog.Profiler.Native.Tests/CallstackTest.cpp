// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPool.h"
#include "Callstack.h"
#include "FixedSizeAllocator.h"

#include "gtest/gtest.h"

TEST(CallstackTest, CheckMoveAssignmentOperator)
{
    auto allocator = FixedSizeAllocator(Callstack::MaxSize, 2);
    CallstackPool p(&allocator);

    auto s = p.Get();

    s.Add(0);
    s.Add(1);
    s.Add(2);
    s.Add(3);

    auto s2 = std::move(s);

    ASSERT_EQ(s.Size(), 0);
    ASSERT_EQ(s.Capacity(), 0);
    ASSERT_EQ(s.Data().data(), nullptr);
    ASSERT_EQ(s.Data().size(), 0);

    ASSERT_EQ(s2.Capacity(), Callstack::MaxFrames);

    auto expectedIp = 0;
    for (auto ip : s2)
    {
        ASSERT_EQ(ip, expectedIp++);
    }
}

TEST(CallstackTest, CheckAddApi)
{
    auto allocator = FixedSizeAllocator(Callstack::MaxSize, 1);
    CallstackPool p(&allocator);

    auto s = p.Get();

    ASSERT_EQ(s.Size(), 0);

    s.Add(0);
    s.Add(1);
    s.Add(2);

    ASSERT_EQ(s.Size(), 3);

    auto expectedIp = 0;
    for (auto ip : s)
    {
        ASSERT_EQ(ip, expectedIp++);
    }
}

TEST(CallstackTest, CheckBufferSetCountApi)
{
    CallstackPool p(pmr::get_default_resource());

    auto s = p.Get();

    ASSERT_EQ(s.Size(), 0);

    auto buffer = s.Data();
    buffer[0] = 0;
    buffer[1] = 1;
    buffer[2] = 2;

    s.SetCount(3);

    ASSERT_EQ(s.Size(), 3);
    auto expectedIp = 0;
    for (auto ip : s)
    {
        ASSERT_EQ(ip, expectedIp++);
    }
}
