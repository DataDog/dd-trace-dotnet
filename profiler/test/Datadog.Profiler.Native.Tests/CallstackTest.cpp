// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Callstack.h"
#include "CallstackProvider.h"

#include "gtest/gtest.h"

#include "MemoryResourceManager.h"

TEST(CallstackTest, CheckMoveAssignmentOperator)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());

    auto s = p.Get();

    s.Add(0);
    s.Add(1);
    s.Add(2);
    s.Add(3);

    auto s2 = std::move(s);

    ASSERT_EQ(s.Size(), 0);
    ASSERT_EQ(s.Capacity(), 0);
    ASSERT_EQ(s.AsSpan().data(), nullptr);
    ASSERT_EQ(s.AsSpan().size(), 0);

    ASSERT_EQ(s2.Capacity(), Callstack::MaxFrames);

    auto expectedIp = 0;
    for (auto ip : s2)
    {
        ASSERT_EQ(ip, expectedIp++);
    }
}

TEST(CallstackTest, CheckAddApi)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());

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
    auto manager = MemoryResourceManager();
    auto p = CallstackProvider(manager.GetSynchronizedPool(1, Callstack::MaxSize));

    auto s = p.Get();

    ASSERT_EQ(s.Size(), 0);

    auto buffer = s.AsSpan();
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


TEST(CallstackTest, CheckCopyFrom)
{
    auto p = CallstackProvider(MemoryResourceManager::GetDefault());

    auto s = p.Get();

    for (int i = 0; i < 100; i++)
    {
        s.Add(i);
    }

    ASSERT_EQ(s.Size(), 100);

    auto s2 = p.Get();

    s2.CopyFrom(s);
    
    ASSERT_EQ(s, s2);
}
