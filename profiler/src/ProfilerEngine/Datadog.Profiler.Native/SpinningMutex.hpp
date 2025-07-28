// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>

#include <signal.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <thread>
#include <unistd.h>

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

// /!\ For now, this class is simple enough to replace the std::mutex used for the
// /!\ stack walk lock. If in the future we need it to be on critical paths,
// /!\ it should be reviewed/improved.

// Its goal is just to make sure we do not use signal-unsafe mechanism with the
// timer_create-based CPU profiler.
// Once the CPU profiler (timer_create) is ready and fully working, we will
// rethink a part of the collection pipeline and the stack walk lock (in ManagedThreadInfo)
// won't be needed (as a lock mechanism)
// So that's why we keep this class simple

class SpinningMutex
{
public:
    SpinningMutex() : _flag(false)
    {
        TSAN_MUTEX_CREATE(this, __tsan_mutex_not_static);
    }

    ~SpinningMutex()
    {
        TSAN_MUTEX_DESTROY(this, __tsan_mutex_not_static);
    }

public:
    void lock()
    {
        try_lock_until_slow(std::chrono::steady_clock::time_point::max());
    }

    template <typename Rep, typename Period>
    bool try_lock_for(std::chrono::duration<Rep, Period> timeout_duration)
    {
        return lock_fast() ? true
                           : try_lock_until_slow(std::chrono::steady_clock::now() +
                                                 timeout_duration);
    }

    bool try_lock()
    {
        TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock);
        auto result = !_flag.exchange(true, std::memory_order_acquire);
        if (result)
        {
            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock, 0);
        }
        else
        {
            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock_failed, 0);
        }
        return result;
    }

    void unlock()
    {
        TSAN_MUTEX_PRE_UNLOCK(this, __tsan_mutex_try_lock);
        _flag.store(false, std::memory_order_release);
        TSAN_MUTEX_POST_UNLOCK(this, __tsan_mutex_try_lock);
    }

private:
    static constexpr uint32_t k_max_active_spin = 4000;
    static constexpr std::chrono::nanoseconds k_yield_sleep =
        std::chrono::microseconds(500);

    bool lock_fast()
    {
        // Taken from
        // https://probablydance.com/2019/12/30/measuring-mutexes-spinlocks-and-how-bad-the-linux-scheduler-really-is/
        uint32_t spincount = 0;

        for (;;)
        {
            TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock);
            if (!_flag.exchange(true, std::memory_order_acquire))
            {
                TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock, 0);
                break;
            }
            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock_failed, 0);
            do
            {
                if (spincount < k_max_active_spin)
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
                    return false;
                }
                // Wait for lock to be released without generating cache misses
            } while (_flag.load(std::memory_order_relaxed));
        }
        return true;
    }

    __attribute__((noinline)) bool
    try_lock_until_slow(std::chrono::steady_clock::time_point timeout_time)
    {
        for (;;)
        {
            TSAN_MUTEX_PRE_LOCK(this, __tsan_mutex_try_lock);
            if (!_flag.exchange(true, std::memory_order_acquire))
            {
                TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock, 0);
                break;
            }

            TSAN_MUTEX_POST_LOCK(this, __tsan_mutex_try_lock_failed, 0);
            do
            {
                if (std::chrono::steady_clock::now() > timeout_time)
                {
                    // timeout
                    return false;
                }
                // If active spin fails, yield
                std::this_thread::sleep_for(k_yield_sleep);

            } while (_flag.load(std::memory_order_relaxed));
        }
        return true;
    }

    std::atomic_bool _flag;
};
