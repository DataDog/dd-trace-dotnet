// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <algorithm>

using namespace std::chrono_literals;

// Encapsulates the cooldown logic for heap snapshot scheduling.
// After a dump completes (or its EventPipe session is cleaned up), a new snapshot
// is blocked until at least `interval` has elapsed.  The test-specific override
// (DD_INTERNAL_PROFILING_TEST_HEAPSNAPSHOT_INTERVAL) feeds into `interval`, so
// short test intervals remain honored.
class SnapshotCooldown
{
public:
    explicit SnapshotCooldown(std::chrono::nanoseconds interval)
        : _interval(interval), _nextAllowed(0ns)
    {
    }

    bool IsAllowed(std::chrono::nanoseconds now) const
    {
        return _nextAllowed == 0ns || now >= _nextAllowed;
    }

    void OnDumpEnd(std::chrono::nanoseconds dumpEndTime)
    {
        _nextAllowed = dumpEndTime + _interval;
    }

    void OnCleanupDone(std::chrono::nanoseconds cleanupTime)
    {
        _nextAllowed = std::max(_nextAllowed, cleanupTime + _interval);
    }

    std::chrono::nanoseconds GetNextAllowed() const { return _nextAllowed; }
    std::chrono::nanoseconds GetInterval() const { return _interval; }

private:
    std::chrono::nanoseconds _interval;
    std::chrono::nanoseconds _nextAllowed;
};
