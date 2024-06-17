// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <condition_variable>
#include <mutex>

class Semaphore
{
public:
    inline Semaphore();
    explicit inline Semaphore(std::uint32_t initialCount);
    inline Semaphore(std::uint32_t initialCount, std::uint32_t maxCount);

    Semaphore(const Semaphore&) = delete;
    Semaphore& operator=(const Semaphore&) = delete;

    inline bool TryAcquire();
    inline std::uint32_t Acquire();
    inline std::uint32_t Release();
    inline std::uint32_t GetCurrentCount() const;
    inline std::uint32_t GetMaxCount() const;

private:
    std::mutex _syncLock;
    std::condition_variable _signal;
    std::uint32_t _currentCount;
    const std::uint32_t _maxCount;
};

class SemaphoreScope
{
public:
    explicit inline SemaphoreScope(Semaphore& semaphore);
    inline ~SemaphoreScope() noexcept;

    SemaphoreScope() = delete;
    SemaphoreScope(const SemaphoreScope&) = delete;
    SemaphoreScope& operator=(const SemaphoreScope&) = delete;

private:
    Semaphore& _semaphore;
};

inline Semaphore::Semaphore() :
    Semaphore(1, 1)
{
}

inline Semaphore::Semaphore(std::uint32_t initialCount) :
    Semaphore(initialCount, initialCount)
{
}

inline Semaphore::Semaphore(std::uint32_t initialCount, std::uint32_t maxCount) :
    _currentCount{initialCount},
    _maxCount{maxCount}
{
}

inline bool Semaphore::TryAcquire()
{
    if (_currentCount > 0)
    {
        std::lock_guard<std::mutex> lock(_syncLock);

        if (_currentCount > 0)
        {
            _currentCount--;
            return true;
        }
    }

    return false;
}

inline std::uint32_t Semaphore::Acquire()
{
    std::unique_lock<std::mutex> lock(_syncLock);

    while (_currentCount == 0)
    {
        _signal.wait(lock);
    }

    return --_currentCount;
}

inline std::uint32_t Semaphore::Release()
{
    std::uint32_t newCount;
    {
        std::lock_guard<std::mutex> lock(_syncLock);
        newCount = ++_currentCount;

        if (_currentCount > _maxCount)
        {
            _currentCount = _maxCount;
        }
    }

    _signal.notify_one();
    return newCount;
}

inline std::uint32_t Semaphore::GetCurrentCount() const
{
    return _currentCount;
}

inline std::uint32_t Semaphore::GetMaxCount() const
{
    return _maxCount;
}

inline SemaphoreScope::SemaphoreScope(Semaphore& semaphore) :
    _semaphore(semaphore)
{
    _semaphore.Acquire();
}

inline SemaphoreScope::~SemaphoreScope() noexcept
{
    _semaphore.Release();
}