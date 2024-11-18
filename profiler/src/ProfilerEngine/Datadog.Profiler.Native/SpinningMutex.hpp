// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <signal.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <thread>
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
        _ownerTid{0}
    {
    }
    ~SpinningMutex() = default;

    SpinningMutex(SpinningMutex const&) = delete;
    SpinningMutex& operator=(SpinningMutex const&) = delete;

    SpinningMutex(SpinningMutex&&) = delete;
    SpinningMutex& operator=(SpinningMutex&&) = delete;

    // make it usable with std::unique_lock
    // so naming will be different from the rest of the code
    //
    // Taken from https://github.com/skarupke/mutex_benchmarks/blob/master/BenchmarkMutex.cpp#L442
    void lock()
    {
        for (;;)
        {
            if (try_lock())
            {
                break;
            }
#ifdef __x86_64__
            asm volatile("pause");
#else
            std::this_thread::yield();
#endif
            if (try_lock())
            {
                break;
            }
            std::this_thread::yield();
        }
    }

    void unlock()
    {
        _ownerTid = 0;
        _locked.clear(std::memory_order_release);
    }

    bool try_lock()
    {
        auto acquired = !_locked.test_and_set(std::memory_order_acquire);
        if (acquired)
        {
            _ownerTid = syscall(SYS_gettid);
        }
        return acquired;
    }

private:
    std::atomic_flag _locked = ATOMIC_FLAG_INIT;
    // mainly for debug reason
    pid_t _ownerTid;
};