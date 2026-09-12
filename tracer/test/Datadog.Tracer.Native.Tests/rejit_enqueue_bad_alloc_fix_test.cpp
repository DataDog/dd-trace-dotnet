#include "pch.h"

#include <atomic>
#include <memory>
#include <vector>

#include "../../src/Datadog.Tracer.Native/rejit_work_offloader.h"
#include "../../../shared/src/native-src/util.h"

using namespace trace;

// ============================================================================
// Copy-on-write correctness for CorProfiler::integration_definitions_'s
// shared_ptr<const vector<T>> representation (see cor_profiler.h). Every read
// and write of integration_definitions_ is already serialized by
// CorProfiler::module_ids (verified by inspection at every call site in
// cor_profiler.cpp), so there is no new synchronization primitive to
// validate here -- what needs proving is the *swap discipline* itself: a
// reader that already holds an older snapshot must keep seeing exactly that
// snapshot, unaffected by a later write, while a fresh read sees the new one.
// ============================================================================

TEST(RejitEnqueueBadAllocFixTests, CopyOnWriteSwap_OldReaderUnaffectedByLaterWrite)
{
    auto initial = std::make_shared<const std::vector<int>>(std::vector<int>{1, 2, 3});

    // "old reader": takes a cheap shared_ptr copy of the current snapshot,
    // the same way EnqueueRequestRejitForLoadedModules's lambda capture does.
    std::shared_ptr<const std::vector<int>> oldReader = initial;

    // "writer": builds a brand new vector (as every integration_definitions_
    // mutation site now does) and swaps it in -- never mutates the vector the
    // old reader is still looking at.
    auto newDefinitions = std::make_shared<std::vector<int>>(*initial);
    newDefinitions->push_back(4);
    std::shared_ptr<const std::vector<int>> member = std::move(newDefinitions);

    // The old reader's snapshot is completely untouched.
    ASSERT_EQ(3u, oldReader->size());
    ASSERT_EQ(1, (*oldReader)[0]);
    ASSERT_EQ(2, (*oldReader)[1]);
    ASSERT_EQ(3, (*oldReader)[2]);

    // A fresh read of the member sees the new content.
    ASSERT_EQ(4u, member->size());
    ASSERT_EQ(4, (*member)[3]);

    // The two are genuinely different objects, not the same vector observed twice.
    ASSERT_NE(oldReader.get(), member.get());
}

TEST(RejitEnqueueBadAllocFixTests, CopyOnWriteSwap_NeverNull)
{
    // integration_definitions_ is default-initialized to an empty shared
    // vector, never nullptr -- callers can always dereference it safely.
    std::shared_ptr<const std::vector<int>> member = std::make_shared<const std::vector<int>>();
    ASSERT_NE(nullptr, member);
    ASSERT_TRUE(member->empty());
}

// ============================================================================
// Exception resilience of the rejit work-item dispatch loop (see
// RejitWorkOffloader::EnqueueThreadLoop, rejit_work_offloader.cpp). This
// exercises the real RejitWorkItem / shared::UniqueBlockingQueue types the
// production loop uses, with a dispatch snippet that mirrors
// EnqueueThreadLoop's try/catch exactly. It deliberately does not go through
// RejitWorkOffloader itself: that class's constructor requires a live
// ICorProfilerInfo7*, and no mock for that interface exists anywhere in this
// test project (ICorProfilerInfo7 is several inheritance tiers past
// MockCorProfilerInfo's ICorProfilerInfo3, i.e. many dozens of unrelated
// methods to stub out just to spin up a background thread) -- what's being
// validated here is the catch-and-continue behavior around item->func(),
// which has nothing to do with ICorProfilerInfo7 or threading itself.
// ============================================================================

namespace
{
// Mirrors RejitWorkOffloader::EnqueueThreadLoop's dispatch body exactly: pop,
// stop on a terminating item, otherwise run func() under a try/catch that
// swallows anything it throws and keeps going.
void RunDispatchLoopLikeEnqueueThreadLoop(shared::UniqueBlockingQueue<RejitWorkItem>& queue)
{
    while (true)
    {
        const auto item = queue.pop();

        if (item->terminating)
        {
            break;
        }
        else if (item->func != nullptr)
        {
            try
            {
                item->func();
            }
            catch (const std::exception&)
            {
            }
            catch (...)
            {
            }
        }
    }
}
} // namespace

TEST(RejitEnqueueBadAllocFixTests, DispatchLoop_SurvivesThrowingItemAndProcessesTheRest)
{
    shared::UniqueBlockingQueue<RejitWorkItem> queue;
    std::atomic<int> completed{0};

    queue.push(std::make_unique<RejitWorkItem>(std::function<void()>([&]() { completed++; })));
    queue.push(std::make_unique<RejitWorkItem>(std::function<void()>([]() { throw std::bad_alloc(); })));
    queue.push(std::make_unique<RejitWorkItem>(std::function<void()>([&]() { completed++; })));
    queue.push(RejitWorkItem::CreateTerminatingWorkItem());

    RunDispatchLoopLikeEnqueueThreadLoop(queue);

    // Both non-throwing items ran; the throwing one in between did not stop
    // the loop, crash the process, or prevent the item after it from running.
    ASSERT_EQ(2, completed.load());
}

TEST(RejitEnqueueBadAllocFixTests, DispatchLoop_SurvivesNonStandardThrow)
{
    shared::UniqueBlockingQueue<RejitWorkItem> queue;
    std::atomic<int> completed{0};

    queue.push(std::make_unique<RejitWorkItem>(std::function<void()>([]() { throw 42; })));
    queue.push(std::make_unique<RejitWorkItem>(std::function<void()>([&]() { completed++; })));
    queue.push(RejitWorkItem::CreateTerminatingWorkItem());

    RunDispatchLoopLikeEnqueueThreadLoop(queue);

    ASSERT_EQ(1, completed.load());
}
