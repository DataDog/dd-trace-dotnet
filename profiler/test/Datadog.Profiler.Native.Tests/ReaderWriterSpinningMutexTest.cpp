// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "ReaderWriterSpinningMutex.hpp"

#include <atomic>
#include <chrono>
#include <future>
#include <iostream>
#include <mutex>
#include <shared_mutex>
#include <signal.h>
#include <thread>
#include <vector>

using namespace std::chrono_literals;

// =======================================================================
// Basic correctness
// =======================================================================

TEST(ReaderWriterSpinningMutexTest, TryLock_WhenFree_Succeeds)
{
    ReaderWriterSpinningMutex mutex;
    EXPECT_TRUE(mutex.try_lock());
    mutex.unlock();
}

TEST(ReaderWriterSpinningMutexTest, TryLock_WhenExclusivelyHeld_Fails)
{
    ReaderWriterSpinningMutex mutex;
    mutex.lock();
    EXPECT_FALSE(mutex.try_lock());
    mutex.unlock();
}

TEST(ReaderWriterSpinningMutexTest, TryLockShared_WhenFree_Succeeds)
{
    ReaderWriterSpinningMutex mutex;
    EXPECT_TRUE(mutex.try_lock_shared());
    mutex.unlock_shared();
}

TEST(ReaderWriterSpinningMutexTest, TryLockShared_WhenExclusivelyHeld_Fails)
{
    ReaderWriterSpinningMutex mutex;
    mutex.lock();
    EXPECT_FALSE(mutex.try_lock_shared());
    mutex.unlock();
}

TEST(ReaderWriterSpinningMutexTest, TryLock_WhenSharedHeld_Fails)
{
    ReaderWriterSpinningMutex mutex;
    mutex.lock_shared();
    EXPECT_FALSE(mutex.try_lock());
    mutex.unlock_shared();
}

TEST(ReaderWriterSpinningMutexTest, MultipleReaders_CanHoldConcurrently)
{
    ReaderWriterSpinningMutex mutex;
    EXPECT_TRUE(mutex.try_lock_shared());
    EXPECT_TRUE(mutex.try_lock_shared());
    EXPECT_TRUE(mutex.try_lock_shared());
    mutex.unlock_shared();
    mutex.unlock_shared();
    mutex.unlock_shared();
}

TEST(ReaderWriterSpinningMutexTest, TryLockFor_WhenExclusivelyHeldByAnotherThread_TimesOutCloseToBudget)
{
    ReaderWriterSpinningMutex mutex;
    std::atomic<bool> writerHoldsLock{false};
    std::atomic<bool> releaseWriter{false};

    std::thread writer([&]() {
        mutex.lock();
        writerHoldsLock.store(true);
        while (!releaseWriter.load())
        {
            std::this_thread::sleep_for(1ms);
        }
        mutex.unlock();
    });

    while (!writerHoldsLock.load())
    {
        std::this_thread::sleep_for(1ms);
    }

    constexpr auto budget = 20ms;
    auto start = std::chrono::steady_clock::now();
    bool acquired = mutex.try_lock_for(budget);
    auto elapsed = std::chrono::steady_clock::now() - start;

    EXPECT_FALSE(acquired);
    // Bounded: must not wait dramatically longer than the requested budget.
    EXPECT_LT(elapsed, budget + 20ms);

    releaseWriter.store(true);
    writer.join();
}

TEST(ReaderWriterSpinningMutexTest, TryLockSharedFor_WhenExclusivelyHeldByAnotherThread_TimesOutCloseToBudget)
{
    ReaderWriterSpinningMutex mutex;
    std::atomic<bool> writerHoldsLock{false};
    std::atomic<bool> releaseWriter{false};

    std::thread writer([&]() {
        mutex.lock();
        writerHoldsLock.store(true);
        while (!releaseWriter.load())
        {
            std::this_thread::sleep_for(1ms);
        }
        mutex.unlock();
    });

    while (!writerHoldsLock.load())
    {
        std::this_thread::sleep_for(1ms);
    }

    constexpr auto budget = 20ms;
    auto start = std::chrono::steady_clock::now();
    bool acquired = mutex.try_lock_shared_for(budget);
    auto elapsed = std::chrono::steady_clock::now() - start;

    EXPECT_FALSE(acquired);
    EXPECT_LT(elapsed, budget + 20ms);

    releaseWriter.store(true);
    writer.join();
}

// Regression test for a real bug found while benchmarking: the internal
// sleep-based backoff (used once the active-spin budget is exhausted)
// unconditionally slept for a fixed ~500us slice regardless of how close the
// deadline already was, so a *short* bounded wait (exactly the
// signal-handler use case this primitive exists for) could overshoot its
// requested budget by nearly that full slice on every failed attempt. That
// was invisible in millisecond-scale timeout tests (500us is noise at that
// scale) but was an 8x+ overshoot for a budget in the tens-of-microseconds
// range - measured via `--gtest_filter=*Benchmark*` on an optimized build.
// Average over many attempts to smooth out one-off scheduling jitter while
// still catching a systematic per-call overshoot.
TEST(ReaderWriterSpinningMutexTest, TryLockSharedFor_ShortBudget_DoesNotSystematicallyOvershoot)
{
    ReaderWriterSpinningMutex mutex;
    mutex.lock(); // Held for the whole test; every attempt below must fail/time out.

    constexpr auto budget = 200us;
    constexpr int iterations = 500;

    auto start = std::chrono::steady_clock::now();
    for (int i = 0; i < iterations; i++)
    {
        ASSERT_FALSE(mutex.try_lock_shared_for(budget));
    }
    auto averagePerCall = (std::chrono::steady_clock::now() - start) / iterations;

    // A regression to the old unconditional-sleep behavior would push this to
    // budget + ~500us (or more) on essentially every call. Give headroom for
    // scheduling jitter while staying far tighter than that.
    EXPECT_LT(averagePerCall, budget + 300us);

    mutex.unlock();
}

TEST(ReaderWriterSpinningMutexTest, TryLockFor_WhenReleasedBeforeTimeout_Succeeds)
{
    ReaderWriterSpinningMutex mutex;
    std::atomic<bool> readerHoldsLock{false};

    // Acquire and release the shared lock on the SAME thread. Doing it across
    // threads (lock_shared() on the main thread, unlock_shared() on another)
    // is legal for this atomic-counter-based design, but it confuses TSAN's
    // built-in deadlock detector, which models mutex ownership per-thread.
    std::thread reader([&]() {
        mutex.lock_shared();
        readerHoldsLock.store(true);
        std::this_thread::sleep_for(10ms);
        mutex.unlock_shared();
    });

    while (!readerHoldsLock.load())
    {
        std::this_thread::sleep_for(1ms);
    }

    EXPECT_TRUE(mutex.try_lock_for(1s));
    mutex.unlock();
    reader.join();
}

// =======================================================================
// RAII wrapper compatibility (std::unique_lock / std::shared_lock)
// =======================================================================

TEST(ReaderWriterSpinningMutexTest, WorksWithStdUniqueLock)
{
    ReaderWriterSpinningMutex mutex;
    {
        std::unique_lock<ReaderWriterSpinningMutex> lock(mutex);
        EXPECT_TRUE(lock.owns_lock());
        EXPECT_FALSE(mutex.try_lock_shared());
    }
    EXPECT_TRUE(mutex.try_lock_shared());
    mutex.unlock_shared();
}

TEST(ReaderWriterSpinningMutexTest, WorksWithStdSharedLock)
{
    ReaderWriterSpinningMutex mutex;
    {
        std::shared_lock<ReaderWriterSpinningMutex> lock(mutex);
        EXPECT_TRUE(lock.owns_lock());
        EXPECT_FALSE(mutex.try_lock());
    }
    EXPECT_TRUE(mutex.try_lock());
    mutex.unlock();
}

TEST(ReaderWriterSpinningMutexTest, WorksWithStdSharedLock_TimedTryLock)
{
    ReaderWriterSpinningMutex mutex;
    mutex.lock();

    std::shared_lock<ReaderWriterSpinningMutex> lock(mutex, 10ms);
    EXPECT_FALSE(lock.owns_lock());

    mutex.unlock();
}

// =======================================================================
// Concurrency stress: invariant must never be violated
// =======================================================================

// Writer updates two related fields under the exclusive lock; readers must
// never observe them out of sync. This would fail under a buggy
// implementation that lets a reader in while a writer is mid-update, or lets
// two writers in at once.
TEST(ReaderWriterSpinningMutexTest, ConcurrentReadersAndWriters_NeverObserveTornState)
{
    ReaderWriterSpinningMutex mutex;
    long long canaryA = 0;
    long long canaryB = 0;
    std::atomic<bool> stop{false};
    std::atomic<int> tornObservations{0};
    std::atomic<int> readCount{0};

    constexpr int numReaders = 8;
    constexpr int numWriters = 2;
    constexpr auto testDuration = 300ms;

    std::vector<std::thread> threads;
    for (int i = 0; i < numReaders; i++)
    {
        threads.emplace_back([&]() {
            while (!stop.load(std::memory_order_relaxed))
            {
                std::shared_lock<ReaderWriterSpinningMutex> lock(mutex);
                if (canaryA != canaryB)
                {
                    tornObservations.fetch_add(1);
                }
                readCount.fetch_add(1, std::memory_order_relaxed);
            }
        });
    }
    for (int i = 0; i < numWriters; i++)
    {
        threads.emplace_back([&]() {
            long long value = 0;
            while (!stop.load(std::memory_order_relaxed))
            {
                std::unique_lock<ReaderWriterSpinningMutex> lock(mutex);
                value++;
                canaryA = value;
                // Small window where, if the lock were broken, a reader could
                // slip in and observe canaryA != canaryB.
                std::this_thread::yield();
                canaryB = value;
            }
        });
    }

    std::this_thread::sleep_for(testDuration);
    stop.store(true, std::memory_order_relaxed);
    for (auto& t : threads)
    {
        t.join();
    }

    EXPECT_EQ(0, tornObservations.load());
    EXPECT_GT(readCount.load(), 0);
}

// =======================================================================
// Writer fairness: a steady stream of readers must not starve a writer.
// =======================================================================

TEST(ReaderWriterSpinningMutexTest, SustainedReaders_DoNotStarveWaitingWriter)
{
    ReaderWriterSpinningMutex mutex;
    std::atomic<bool> stop{false};

    constexpr int numReaders = 8;
    std::vector<std::thread> readers;
    for (int i = 0; i < numReaders; i++)
    {
        readers.emplace_back([&]() {
            while (!stop.load(std::memory_order_relaxed))
            {
                if (mutex.try_lock_shared_for(1ms))
                {
                    // Simulate a tiny bit of read work.
                    std::this_thread::yield();
                    mutex.unlock_shared();
                }
            }
        });
    }

    // Give the readers a head start so there is real contention.
    std::this_thread::sleep_for(20ms);

    auto start = std::chrono::steady_clock::now();
    mutex.lock();
    auto elapsed = std::chrono::steady_clock::now() - start;
    mutex.unlock();

    stop.store(true, std::memory_order_relaxed);
    for (auto& t : readers)
    {
        t.join();
    }

    // With the "waiting writer" fairness flag, the writer must not be starved
    // indefinitely by a continuous stream of short-lived readers.
    EXPECT_LT(elapsed, 2s);
}

// =======================================================================
// Real-signal reentrancy: the scenario this primitive exists for.
// =======================================================================
// A thread takes the exclusive lock, then raises a real SIGUSR1 against
// itself while still holding it (mirroring the CPU/wall-time profiler
// delivering its sampling signal to the very thread being sampled). The
// signal handler tries to take the shared lock with a bounded timeout.
// This must never hang: try_lock_shared_for must return promptly (it will
// return false, since this lock is not reentrant), and control must return
// to the interrupted writer, which then completes normally.

namespace
{
ReaderWriterSpinningMutex* g_signalTestMutex = nullptr;
std::atomic<bool> g_handlerRan{false};
std::atomic<bool> g_handlerAcquiredSharedLock{false};

void SelfSignalReentrancyHandler(int)
{
    g_handlerRan.store(true, std::memory_order_relaxed);
    bool acquired = g_signalTestMutex->try_lock_shared_for(std::chrono::milliseconds(50));
    if (acquired)
    {
        g_signalTestMutex->unlock_shared();
    }
    g_handlerAcquiredSharedLock.store(acquired, std::memory_order_relaxed);
}
} // namespace

TEST(ReaderWriterSpinningMutexTest, WriterInterruptedBySelfSignal_DoesNotDeadlock)
{
    ReaderWriterSpinningMutex mutex;
    g_signalTestMutex = &mutex;
    g_handlerRan.store(false);
    g_handlerAcquiredSharedLock.store(false);

    struct sigaction action{};
    action.sa_handler = SelfSignalReentrancyHandler;
    sigemptyset(&action.sa_mask);
    action.sa_flags = 0;

    struct sigaction previousAction{};
    ASSERT_EQ(0, sigaction(SIGUSR1, &action, &previousAction));

    // Run the actual reentrancy scenario on a worker thread so the test can
    // apply a watchdog timeout around it: if a regression ever turns this
    // primitive into something that can truly hang on self-reentrancy, the
    // test fails loudly instead of hanging CI silently.
    auto future = std::async(std::launch::async, [&]() {
        mutex.lock();
        // Deliver SIGUSR1 to this very thread while still holding the
        // exclusive lock - this is the exact hazard the design doc calls
        // out (a profiler signal targeted at the thread that is currently
        // writing to the cache).
        pthread_kill(pthread_self(), SIGUSR1);
        mutex.unlock();
    });

    auto status = future.wait_for(5s);
    sigaction(SIGUSR1, &previousAction, nullptr);

    ASSERT_EQ(std::future_status::ready, status) << "writer thread appears stuck: self-signal reentrancy caused a hang";
    EXPECT_TRUE(g_handlerRan.load());
    // The lock is not reentrant, so the handler must have backed off rather
    // than acquiring it while the same thread holds the exclusive lock.
    EXPECT_FALSE(g_handlerAcquiredSharedLock.load());

    // The mutex must be left in a clean, reusable state afterwards.
    EXPECT_TRUE(mutex.try_lock());
    mutex.unlock();

    g_signalTestMutex = nullptr;
}

// =======================================================================
// Micro-benchmarks (disabled by default - run explicitly with
// --gtest_also_run_disabled_tests --gtest_filter=*Benchmark*)
// =======================================================================
// Not a substitute for a real benchmark harness, but enough to sanity-check
// that ReaderWriterSpinningMutex is in the right ballpark versus the
// std::shared_timed_mutex baseline it is meant to replace, both uncontended
// (dominated by the primitive's own overhead) and under writer contention
// (where std::shared_timed_mutex's try_lock_for goes through libstdc++'s
// internal condition-variable wait, while ReaderWriterSpinningMutex spins/sleeps
// on plain atomics).
//
// IMPORTANT - build configuration matters a lot here: this repo's local
// CMake dev build defaults to CMAKE_BUILD_TYPE=Debug (effectively -O0). At
// -O0, ReaderWriterSpinningMutex's header-only call chain (lock_shared() ->
// try_lock_shared() -> try_lock_shared_once(), etc.) is NOT inlined and pays
// real function-call overhead on every operation, whereas
// std::shared_timed_mutex's pthread_rwlock_* implementation is already
// compiled into libstdc++.so at full optimization regardless of how this
// test binary is built. That asymmetry alone was measured to make the
// uncontended benchmark look ~1.4-1.8x slower for ReaderWriterSpinningMutex at
// -O0. Build with -DCMAKE_BUILD_TYPE=Release (or any -O1/-O2/-O3) to get a
// representative comparison: with real inlining, ReaderWriterSpinningMutex is
// measured to be ~25-30% FASTER than std::shared_timed_mutex uncontended,
// and roughly on par under writer contention with a short timeout budget.

namespace
{
template <typename Func>
double MeasureNanosPerOp(int iterations, Func&& func)
{
    auto start = std::chrono::steady_clock::now();
    for (int i = 0; i < iterations; i++)
    {
        func();
    }
    auto elapsed = std::chrono::steady_clock::now() - start;
    return static_cast<double>(std::chrono::duration_cast<std::chrono::nanoseconds>(elapsed).count()) / iterations;
}
} // namespace

TEST(ReaderWriterSpinningMutexTest, DISABLED_Benchmark_UncontendedSharedLockUnlock)
{
    constexpr int iterations = 2'000'000;

    ReaderWriterSpinningMutex spinMutex;
    auto spinNs = MeasureNanosPerOp(iterations, [&]() {
        spinMutex.lock_shared();
        spinMutex.unlock_shared();
    });

    std::shared_timed_mutex stdMutex;
    auto stdNs = MeasureNanosPerOp(iterations, [&]() {
        stdMutex.lock_shared();
        stdMutex.unlock_shared();
    });

    std::cout << "[benchmark] uncontended lock_shared+unlock_shared: "
              << "ReaderWriterSpinningMutex=" << spinNs << " ns/op, "
              << "std::shared_timed_mutex=" << stdNs << " ns/op" << std::endl;
}

TEST(ReaderWriterSpinningMutexTest, DISABLED_Benchmark_ReaderLatencyUnderWriterContention)
{
    // A writer holds the exclusive lock for a short window in a tight loop;
    // meanwhile a reader repeatedly tries a bounded try_lock_shared_for and
    // we measure how long each *failing* attempt takes to give up - this is
    // exactly the ManagedCodeCache signal-handler scenario (bounded wait,
    // give up cleanly rather than block forever).
    constexpr auto perAttemptBudget = 50us;
    constexpr int iterations = 2000;

    auto benchmarkSpin = [&]() {
        ReaderWriterSpinningMutex mutex;
        std::atomic<bool> stop{false};
        std::thread writer([&]() {
            while (!stop.load(std::memory_order_relaxed))
            {
                mutex.lock();
                std::this_thread::sleep_for(10us);
                mutex.unlock();
            }
        });

        auto ns = MeasureNanosPerOp(iterations, [&]() {
            if (mutex.try_lock_shared_for(perAttemptBudget))
            {
                mutex.unlock_shared();
            }
        });

        stop.store(true, std::memory_order_relaxed);
        writer.join();
        return ns;
    };

    auto benchmarkStd = [&]() {
        std::shared_timed_mutex mutex;
        std::atomic<bool> stop{false};
        std::thread writer([&]() {
            while (!stop.load(std::memory_order_relaxed))
            {
                mutex.lock();
                std::this_thread::sleep_for(10us);
                mutex.unlock();
            }
        });

        auto ns = MeasureNanosPerOp(iterations, [&]() {
            if (mutex.try_lock_shared_for(perAttemptBudget))
            {
                mutex.unlock_shared();
            }
        });

        stop.store(true, std::memory_order_relaxed);
        writer.join();
        return ns;
    };

    auto spinNs = benchmarkSpin();
    auto stdNs = benchmarkStd();

    std::cout << "[benchmark] try_lock_shared_for(" << perAttemptBudget.count() << "us) under writer contention: "
              << "ReaderWriterSpinningMutex=" << spinNs << " ns/op, "
              << "std::shared_timed_mutex=" << stdNs << " ns/op" << std::endl;
}
