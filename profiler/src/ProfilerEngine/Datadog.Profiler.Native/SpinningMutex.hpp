// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <sched.h>
#include <signal.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <time.h>
#include <unistd.h>

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
    SpinningMutex() :
        _isLockTaken{0}, _ownerTid{0} {};
    ~SpinningMutex() = default;

    SpinningMutex(SpinningMutex const&) = delete;
    SpinningMutex& operator=(SpinningMutex const&) = delete;

    SpinningMutex(SpinningMutex&&) = delete;
    SpinningMutex& operator=(SpinningMutex&&) = delete;

    // make it usable with std::unique_lock
    // so naming will be different from the rest of the code
    void lock()
    {
        for (int spin_count = 0; !try_lock(); ++spin_count)
        {
#ifdef __x86_64__
            // 16 taken from https://probablydance.com/2019/12/30/measuring-mutexes-spinlocks-and-how-bad-the-linux-scheduler-really-is/
            if (spin_count < 16)
            {
                asm volatile("pause");
            }
            else
            {
                sched_yield();
                spin_count = 0;
            }
#else
            asm volatile("yield");
#endif
        }
    }

    void unlock()
    {
        _ownerTid = 0;
        __sync_lock_release(&_isLockTaken);
    }

    bool try_lock()
    {
        auto acquired = __sync_lock_test_and_set(&_isLockTaken, 1) == 0;
        if (acquired)
        {
            _ownerTid = syscall(SYS_gettid);
        }
        return acquired;
    }

private:
    volatile sig_atomic_t _isLockTaken;
    // mainly for debug reason
    pid_t _ownerTid;
};