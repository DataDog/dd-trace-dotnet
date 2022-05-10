// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"
#include "gmock/gmock.h"
#include <thread>

#include "ManagedThreadList.h"
#include "ManagedThreadInfo.h"


TEST(ManagedThreadListTest, CheckAdd)
{
    ManagedThreadList threads(nullptr);
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);

    ASSERT_EQ(threads.Count(), 3);
}

TEST(ManagedThreadListTest, CheckRemove)
{
    ManagedThreadList threads(nullptr);
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);

    ASSERT_EQ(threads.Count(), 3);

    bool found = false;
    ManagedThreadInfo* pInfo = nullptr;

    // ok to remove an existing thread
    found = threads.UnregisterThread(1, &pInfo);
    ASSERT_TRUE(found);
    ASSERT_NE(pInfo, nullptr);

    // fail to remove an unknown thread
    found = threads.UnregisterThread(4, &pInfo);
    ASSERT_FALSE(found);
}

TEST(ManagedThreadListTest, CheckLoopNext)
{
    ManagedThreadList threads(nullptr);
    auto iterator = threads.CreateIterator();

    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);

    // check iterator
    ManagedThreadInfo* pInfo = nullptr;
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 1);
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 2);
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 3);

    // should restart from the beginning
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 1);

    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 2);
}

TEST(ManagedThreadListTest, CheckLoopNextWhenAdd)
{
    ManagedThreadList threads(nullptr);
    auto iterator = threads.CreateIterator();
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);

    // add during iteration
    ManagedThreadInfo* pInfo = nullptr;
    pInfo = threads.LoopNext(iterator);
    pInfo = threads.LoopNext(iterator);
    threads.GetOrCreateThread(4);

    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 3);
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 4);
}

TEST(ManagedThreadListTest, CheckLoopNextWhenRemove)
{
    ManagedThreadList threads(nullptr);
    auto iterator = threads.CreateIterator();
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);
    threads.GetOrCreateThread(4);
    threads.GetOrCreateThread(5);

    ManagedThreadInfo* pInfo = nullptr;
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 1);
    //
    // see what happens when an element is removed after and before the current position
    //    1   2   3   4   5
    //        ^

    // after current position --> 2
    threads.UnregisterThread(3, &pInfo);
    //    1   2   x   4   5
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 2);
    //    1   2   x   4   5
    //                ^

    // current position --> 5
    threads.UnregisterThread(4, &pInfo);
    //    1   2   x   x   5
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 5);
    //    1   2   x   x   5
    //    ^

    // before current position -->
    pInfo = threads.LoopNext(iterator);
    //    1   2   x   x   5
    //        ^
    threads.UnregisterThread(1, &pInfo);
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 2);
    //    x   2   x   x   5
    //                    ^
}

TEST(ManagedThreadListTest, CheckLoopNextWhenRemoveLastThread)
{
    ManagedThreadList threads(nullptr);
    auto iterator = threads.CreateIterator();
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);

    ManagedThreadInfo* pInfo = nullptr;
    pInfo = threads.LoopNext(iterator);
    pInfo = threads.LoopNext(iterator);
    //
    // see what happens when an element is removed after and before the current position
    //    1   2   3
    //            ^

    // remove last position --> 0
    threads.UnregisterThread(3, &pInfo);
    //    1   2   x
    //    ^
    pInfo = threads.LoopNext(iterator);
    ASSERT_TRUE(pInfo != nullptr);
    ASSERT_EQ(pInfo->GetClrThreadId(), 1);
}


class MultipleIteratorsParams
{
public:
    MultipleIteratorsParams(ManagedThreadList& threads, uint32_t iterator) :
        Threads{threads},
        Iterator{iterator}
    {
    }

public:
    ManagedThreadList& Threads;
    uint32_t Iterator;
};

void WorkOnIterator(MultipleIteratorsParams* parameters)
{
    ManagedThreadInfo* pInfo = nullptr;
    auto& threads = parameters->Threads;
    auto iterator = parameters->Iterator;

    for (size_t i = 0; i < 100000; i++)
    {
        pInfo = threads.LoopNext(iterator);
        ASSERT_EQ(pInfo->GetClrThreadId(), 1);
        pInfo = threads.LoopNext(iterator);
        ASSERT_EQ(pInfo->GetClrThreadId(), 2);
        pInfo = threads.LoopNext(iterator);
        ASSERT_EQ(pInfo->GetClrThreadId(), 3);
        pInfo = threads.LoopNext(iterator);
        ASSERT_EQ(pInfo->GetClrThreadId(), 4);
        pInfo = threads.LoopNext(iterator);
        ASSERT_EQ(pInfo->GetClrThreadId(), 5);

        // should restart from the beginning
    }
}

TEST(ManagedThreadListTest, CheckMultipleIterators)
{
    ManagedThreadList threads(nullptr);
    threads.GetOrCreateThread(1);
    threads.GetOrCreateThread(2);
    threads.GetOrCreateThread(3);
    threads.GetOrCreateThread(4);
    threads.GetOrCreateThread(5);
    MultipleIteratorsParams params1(threads, threads.CreateIterator());
    MultipleIteratorsParams params2(threads, threads.CreateIterator());

    // check that iterators are not overlapping
    std::thread t1(WorkOnIterator, &params1);
    std::thread t2(WorkOnIterator, &params2);
    t1.join();
    t2.join();

    ASSERT_TRUE(true);
}
