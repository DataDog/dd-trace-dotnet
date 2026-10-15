// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "EngineActiveGuard.h"

#include <atomic>
#include <chrono>
#include <future>
#include <memory>
#include <shared_mutex>
#include <thread>

using namespace std::chrono_literals;

// These tests cover the fix for a shutdown-time use-after-free: an ICorProfilerCallback method
// used to check _isInitialized.load() - a bare, lock-free atomic read - then, several lines
// later, dereference a service pointer that CorProfilerCallback::DisposeInternal() could
// concurrently null out and destroy on another thread. A real crash (ExecutionEngineException,
// a ThreadsCpuManager use-after-free deep inside std::unordered_map's internals) was traced to
// exactly this gap: the check and the use were never serialized against each other at all.
//
// EngineActiveGuard replaces that single check with two: isInitialized (has Initialize()
// finished? - a plain atomic read is fine, this direction never destroys anything concurrently)
// and isServicesShutdown (has teardown started? - only ever answered while holding the same
// mutex the teardown path holds exclusively). These tests exercise both halves directly, without
// needing a full CorProfilerCallback.

TEST(EngineActiveGuardTest, ActiveWhenInitializedNotShutdownAndUncontended)
{
    std::atomic<bool> isInitialized{true};
    std::shared_mutex mutex;
    bool isServicesShutdown = false;

    EngineActiveGuard guard(isInitialized, mutex, isServicesShutdown);

    ASSERT_TRUE(guard.IsActive());
}

TEST(EngineActiveGuardTest, NotActiveBeforeInitialization)
{
    // Mirrors the "has not yet initialized" half of the original
    // `if (false == _isInitialized.load()) return S_OK;` check - a callback firing before
    // Initialize() has finished constructing the services must still no-op, exactly as before.
    std::atomic<bool> isInitialized{false};
    std::shared_mutex mutex;
    bool isServicesShutdown = false;

    EngineActiveGuard guard(isInitialized, mutex, isServicesShutdown);

    ASSERT_FALSE(guard.IsActive());
}

TEST(EngineActiveGuardTest, NotActiveOnceServicesShutdownFlagIsSet)
{
    std::atomic<bool> isInitialized{true};
    std::shared_mutex mutex;
    bool isServicesShutdown = true;

    // No writer holds the mutex here - a bare try_to_lock would succeed. This is exactly the gap
    // the fix closes: acquiring the lock is not enough on its own, IsActive() must also observe
    // isServicesShutdown while still holding it, or a callback arriving right after teardown
    // releases the lock would sail through and touch already-destroyed service pointers.
    EngineActiveGuard guard(isInitialized, mutex, isServicesShutdown);

    ASSERT_FALSE(guard.IsActive());
}

TEST(EngineActiveGuardTest, NotActiveWhileWriterHoldsTheExclusiveLock)
{
    std::atomic<bool> isInitialized{true};
    std::shared_mutex mutex;
    bool isServicesShutdown = false;

    std::promise<void> writerHasTheLock;
    std::promise<void> readerHasChecked;
    auto readerHasCheckedFuture = readerHasChecked.get_future();

    // Mirrors DisposeInternal() holding the exclusive lock for the duration of DisposeServices().
    std::thread writer(
        [&]
        {
            std::unique_lock<std::shared_mutex> exclusiveLock(mutex);
            writerHasTheLock.set_value();
            readerHasCheckedFuture.wait();
        });

    writerHasTheLock.get_future().wait();

    // A callback arriving while teardown is in progress must fail to acquire the lock at all
    // (non-blocking try_to_lock), not block waiting for it - callbacks must never wait.
    EngineActiveGuard guard(isInitialized, mutex, isServicesShutdown);
    ASSERT_FALSE(guard.IsActive());

    readerHasChecked.set_value();
    writer.join();
}

TEST(EngineActiveGuardTest, WriterBlocksUntilReaderGuardIsReleased)
{
    std::atomic<bool> isInitialized{true};
    std::shared_mutex mutex;
    bool isServicesShutdown = false;

    auto guard = std::make_unique<EngineActiveGuard>(isInitialized, mutex, isServicesShutdown);
    ASSERT_TRUE(guard->IsActive());

    std::atomic<bool> writerAcquired{false};
    std::thread writer(
        [&]
        {
            std::unique_lock<std::shared_mutex> exclusiveLock(mutex);
            writerAcquired.store(true);
        });

    // The writer (teardown) must not be able to proceed while an in-flight callback still holds
    // its shared lock - this is what guarantees a callback that already passed its guard check
    // gets to finish using service pointers before DisposeServices() can destroy them.
    std::this_thread::sleep_for(50ms);
    ASSERT_FALSE(writerAcquired.load());

    guard.reset(); // release the reader's shared lock
    writer.join();

    ASSERT_TRUE(writerAcquired.load());
}

TEST(EngineActiveGuardTest, MultipleReadersCanBeActiveConcurrently)
{
    std::atomic<bool> isInitialized{true};
    std::shared_mutex mutex;
    bool isServicesShutdown = false;

    // Callbacks legitimately run concurrently on different threads in normal operation - the
    // guard must not serialize them against each other, only against the writer.
    EngineActiveGuard first(isInitialized, mutex, isServicesShutdown);
    EngineActiveGuard second(isInitialized, mutex, isServicesShutdown);

    ASSERT_TRUE(first.IsActive());
    ASSERT_TRUE(second.IsActive());
}
