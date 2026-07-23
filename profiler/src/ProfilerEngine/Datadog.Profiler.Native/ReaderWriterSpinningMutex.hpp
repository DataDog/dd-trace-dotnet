// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <cassert>
#include <chrono>

#include <errno.h>
#include <time.h>

#ifdef DD_SANITIZE_THREAD
#include <sanitizer/tsan_interface.h>
#define TSAN_MUTEX_CREATE __tsan_mutex_create
#define TSAN_MUTEX_DESTROY __tsan_mutex_destroy
#define TSAN_MUTEX_PRE_LOCK __tsan_mutex_pre_lock
#define TSAN_MUTEX_POST_LOCK __tsan_mutex_post_lock
#define TSAN_MUTEX_PRE_UNLOCK __tsan_mutex_pre_unlock
#define TSAN_MUTEX_POST_UNLOCK __tsan_mutex_post_unlock
#else
#define TSAN_MUTEX_CREATE(...)
#define TSAN_MUTEX_DESTROY(...)
#define TSAN_MUTEX_PRE_LOCK(...)
#define TSAN_MUTEX_POST_LOCK(...)
#define TSAN_MUTEX_PRE_UNLOCK(...)
#define TSAN_MUTEX_POST_UNLOCK(...)
#endif

// ReaderWriterSpinningMutex
// =====================
//
// A reader-writer lock built entirely out of std::atomic operations: no
// pthread mutex, no condition variable, no futex/syscall in the fast path.
// It is meant as a signal-handler-safe alternative to std::shared_mutex /
// std::shared_timed_mutex for code that may be read from inside a POSIX
// signal handler (e.g. the timer_create-based CPU profiler's SIGPROF handler,
// or the wall-time profiler's SIGUSR1 handler).
//
// It satisfies (a relevant subset of) the SharedMutex/SharedTimedMutex named
// requirements, so it can be used directly with std::shared_lock<T> and
// std::unique_lock<T>.
//
// Design
// ------
// A single state word encodes the lock:
//   0  -> unlocked
//   -1 -> exclusively locked (by a writer)
//   N (N > 0) -> N concurrent readers
//
// A separate "waiting writer" counter is used purely for fairness: once at
// least one writer is waiting, new readers back off instead of joining, so a
// steady stream of readers cannot starve a writer forever. This does NOT
// change correctness (readers already holding the lock are unaffected), only
// how quickly a pending writer can get in.
//
// Safety note
// -----------
// This primitive only makes the *lock itself* signal-safe (bounded; the
// slow path's backoff sleep goes straight to ::clock_nanosleep() rather than
// std::this_thread::sleep_for(), see BoundedSleep() below for why). It does
// NOT by itself prevent the classic same-thread reentrancy deadlock (writer
// holds the lock, gets interrupted by a signal targeted at the very same
// thread, and the handler tries to take the lock too). Bounded
// try_lock_for/try_lock_shared_for calls turn that
// scenario into "handler gives up after the timeout" rather than "hang
// forever", which is the same trade-off already made by ManagedCodeCache
// today. If true reentrancy-proof behavior is required, combine this with
// blocking the relevant signals for the duration of the write (see
// ScopedProfilerSignalBlocker in ManagedCodeCache.cpp) or avoid the lock
// entirely on the read path (we could seek for a lock-free append-only design).
class ReaderWriterSpinningMutex
{
public:
    ReaderWriterSpinningMutex() noexcept :
        _state(0),
        _waitingWriters(0)
    {
        TSAN_MUTEX_CREATE(this, __tsan_mutex_not_static);
    }

    ~ReaderWriterSpinningMutex()
    {
        TSAN_MUTEX_DESTROY(this, __tsan_mutex_not_static);
    }

    ReaderWriterSpinningMutex(const ReaderWriterSpinningMutex&) = delete;
    ReaderWriterSpinningMutex& operator=(const ReaderWriterSpinningMutex&) = delete;
    ReaderWriterSpinningMutex(ReaderWriterSpinningMutex&&) = delete;
    ReaderWriterSpinningMutex& operator=(ReaderWriterSpinningMutex&&) = delete;

    // -------------------------------------------------------------------
    // Exclusive (writer) API
    // -------------------------------------------------------------------

    void lock() noexcept
    {
        // Fast path: skip the (noinline) slow path and the waiting-writer
        // bookkeeping entirely when the lock is immediately available, which
        // is the common case for short-lived writer critical sections.
        if (try_lock())
        {
            return;
        }

        WaitingWriterGuard waitingGuard(_waitingWriters);
        try_lock_exclusive_until_slow(std::chrono::steady_clock::time_point::max());
    }

    template <typename Rep, typename Period>
    bool try_lock_for(std::chrono::duration<Rep, Period> timeout_duration) noexcept
    {
        if (try_lock())
        {
            return true;
        }

        WaitingWriterGuard waitingGuard(_waitingWriters);
        return try_lock_exclusive_until_slow(std::chrono::steady_clock::now() + timeout_duration);
    }

    template <typename Clock, typename Duration>
    bool try_lock_until(std::chrono::time_point<Clock, Duration> timeout_time) noexcept
    {
        return try_lock_for(timeout_time - Clock::now());
    }

    bool try_lock() noexcept
    {
        TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock);
        int32_t expected = kFree;
        bool result = _state.compare_exchange_strong(expected, kExclusive,
                                                       std::memory_order_acquire,
                                                       std::memory_order_relaxed);
        TSAN_MUTEX_POST_LOCK(this, result ? __tsan_mutex_try_lock : __tsan_mutex_try_lock_failed, 0);
        return result;
    }

    void unlock() noexcept
    {
        TSAN_MUTEX_PRE_UNLOCK(this, 0);
        assert(_state.load(std::memory_order_relaxed) == kExclusive);
        _state.store(kFree, std::memory_order_release);
        TSAN_MUTEX_POST_UNLOCK(this, 0);
    }

    // -------------------------------------------------------------------
    // Shared (reader) API
    // -------------------------------------------------------------------

    void lock_shared() noexcept
    {
        // Fast path: same rationale as lock() above.
        if (try_lock_shared())
        {
            return;
        }

        try_lock_shared_until_slow(std::chrono::steady_clock::time_point::max());
    }

    template <typename Rep, typename Period>
    bool try_lock_shared_for(std::chrono::duration<Rep, Period> timeout_duration) noexcept
    {
        if (try_lock_shared())
        {
            return true;
        }
        return try_lock_shared_until_slow(std::chrono::steady_clock::now() + timeout_duration);
    }

    template <typename Clock, typename Duration>
    bool try_lock_shared_until(std::chrono::time_point<Clock, Duration> timeout_time) noexcept
    {
        return try_lock_shared_for(timeout_time - Clock::now());
    }

    bool try_lock_shared() noexcept
    {
        TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock | __tsan_mutex_read_lock);
        // Fairness: honor the waiting-writer flag here too, not just in the
        // slow (blocking) path below. Without this, a tight retry loop of
        // plain try_lock_shared() calls (which is exactly what a spinning
        // reader does) would always win the fast path and could starve a
        // waiting writer indefinitely.
        // relaxed is enough: _waitingWriters is purely an advisory fairness
        // hint (it doesn't guard any other memory), so a stale read only
        // means a reader occasionally wins one extra race before backing
        // off - a liveness nicety, not a safety property. Correctness is
        // fully governed by the CAS on _state in try_lock_shared_once().
        bool result = _waitingWriters.load(std::memory_order_relaxed) == 0 && try_lock_shared_once();
        TSAN_MUTEX_POST_LOCK(this, (result ? __tsan_mutex_try_lock : __tsan_mutex_try_lock_failed) | __tsan_mutex_read_lock, 0);
        return result;
    }

    void unlock_shared() noexcept
    {
        TSAN_MUTEX_PRE_UNLOCK(this, __tsan_mutex_read_lock);
        [[maybe_unused]] auto previous = _state.fetch_sub(1, std::memory_order_release);
        assert(previous > kFree);
        TSAN_MUTEX_POST_UNLOCK(this, __tsan_mutex_read_lock);
    }

    // Exposed for tests only: number of writers currently blocked in lock()/
    // try_lock_for(). Used to assert on fairness/starvation behavior.
    int32_t WaitingWriterCountForTest() const noexcept
    {
        return _waitingWriters.load(std::memory_order_relaxed);
    }

private:
    static constexpr int32_t kFree = 0;
    static constexpr int32_t kExclusive = -1;

    static constexpr uint32_t kMaxActiveSpin = 4000;
    static constexpr std::chrono::nanoseconds kYieldSleep = std::chrono::microseconds(500);

    // RAII helper: increments the waiting-writer counter for the lifetime of
    // a blocking lock() / try_lock_for() call, regardless of how it exits
    // (success or timeout), so readers can see "a writer is waiting" and back
    // off without the counter ever leaking upward.
    struct WaitingWriterGuard
    {
        explicit WaitingWriterGuard(std::atomic<int32_t>& counter) noexcept : _counter(counter)
        {
            _counter.fetch_add(1, std::memory_order_relaxed);
        }

        ~WaitingWriterGuard()
        {
            _counter.fetch_sub(1, std::memory_order_relaxed);
        }

        WaitingWriterGuard(const WaitingWriterGuard&) = delete;
        WaitingWriterGuard& operator=(const WaitingWriterGuard&) = delete;

        std::atomic<int32_t>& _counter;
    };

    // Sleeps for at most `duration` without going through
    // std::this_thread::sleep_for(): this primitive can be exercised from a
    // POSIX signal handler (that is its whole purpose), and sleep_for() is
    // not documented as async-signal-safe - on this toolchain it is a thin
    // wrapper around ::nanosleep(), which POSIX explicitly does NOT put on
    // the async-signal-safe list (see signal-safety(7); only bare sleep(3)
    // is listed, not nanosleep(2)/clock_nanosleep(2)). Calling
    // ::clock_nanosleep() directly is not a POSIX-certified guarantee either
    // (no bounded-sleep primitive is), but it avoids any indirection through
    // libstdc++'s <thread> internals (chrono conversions, its own possible
    // lazy-init) and, unlike ::nanosleep(), reports errors via its return
    // value rather than errno, so this call touches no thread-global state
    // at all beyond the syscall itself.
    static void BoundedSleep(std::chrono::steady_clock::duration duration) noexcept
    {
        if (duration <= std::chrono::steady_clock::duration::zero())
        {
            return;
        }

        auto seconds = std::chrono::duration_cast<std::chrono::seconds>(duration);
        auto nanoseconds = std::chrono::duration_cast<std::chrono::nanoseconds>(duration - seconds);

        struct timespec ts;
        ts.tv_sec = static_cast<time_t>(seconds.count());
        ts.tv_nsec = static_cast<long>(nanoseconds.count());

        // On EINTR, clock_nanosleep() refreshes ts in place with the
        // remaining relative time, so re-issuing the call with the same ts
        // resumes waiting for what is left rather than restarting the full
        // duration.
        while (::clock_nanosleep(CLOCK_MONOTONIC, 0, &ts, &ts) == EINTR)
        {
        }
    }

    bool try_lock_shared_once() noexcept
    {
        int32_t expected = _state.load(std::memory_order_relaxed);
        while (expected >= kFree)
        {
            if (_state.compare_exchange_weak(expected, expected + 1,
                                              std::memory_order_acquire,
                                              std::memory_order_relaxed))
            {
                return true;
            }
            // expected has been updated by compare_exchange_weak; retry unless
            // a writer holds the lock.
        }
        return false;
    }

    __attribute__((noinline)) bool try_lock_exclusive_until_slow(std::chrono::steady_clock::time_point timeoutTime) noexcept
    {
        uint32_t spincount = 0;
        for (;;)
        {
            TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock);
            int32_t expected = kFree;
            if (_state.compare_exchange_weak(expected, kExclusive,
                                              std::memory_order_acquire,
                                              std::memory_order_relaxed))
            {
                TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock, 0);
                return true;
            }
            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock_failed, 0);

            if (spincount < kMaxActiveSpin)
            {
                ++spincount;
#ifdef __x86_64__
                asm volatile("pause");
#else
                asm volatile("yield");
#endif
            }
            else
            {
                auto now = std::chrono::steady_clock::now();
                if (now >= timeoutTime)
                {
                    return false;
                }
                // Clamp to the remaining budget: unconditionally sleeping the
                // full kYieldSleep here would let a short deadline (e.g. a
                // signal handler's bounded try_lock_for) overshoot by nearly
                // kYieldSleep, since the next deadline check only happens
                // after waking up.
                BoundedSleep(std::min<std::chrono::steady_clock::duration>(kYieldSleep, timeoutTime - now));
            }
        }
    }

    __attribute__((noinline)) bool try_lock_shared_until_slow(std::chrono::steady_clock::time_point timeoutTime) noexcept
    {
        uint32_t spincount = 0;
        for (;;)
        {
            TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock | __tsan_mutex_read_lock);
            // Fairness: if a writer is already waiting, do not let new readers
            // jump the queue; only readers already holding the lock can keep
            // going. This bounds how long a writer can be starved.
            // See try_lock_shared() above for why relaxed is sufficient here.
            if (_waitingWriters.load(std::memory_order_relaxed) == 0 && try_lock_shared_once())
            {
                // Must echo __tsan_mutex_try_lock here too (not just
                // read_lock): pre_lock announced this as a try-lock attempt,
                // and TSAN requires that flag to match on the post_lock call
                // that reports its outcome - see try_lock_shared() above,
                // which does the same on its success path.
                TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock | __tsan_mutex_read_lock, 0);
                return true;
            }
            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock_failed | __tsan_mutex_read_lock, 0);

            if (spincount < kMaxActiveSpin)
            {
                ++spincount;
#ifdef __x86_64__
                asm volatile("pause");
#else
                asm volatile("yield");
#endif
            }
            else
            {
                auto now = std::chrono::steady_clock::now();
                if (now >= timeoutTime)
                {
                    return false;
                }
                // See try_lock_exclusive_until_slow() above for why this is
                // clamped rather than an unconditional sleep_for(kYieldSleep).
                BoundedSleep(std::min<std::chrono::steady_clock::duration>(kYieldSleep, timeoutTime - now));
            }
        }
    }

    std::atomic<int32_t> _state;
    std::atomic<int32_t> _waitingWriters;
};
