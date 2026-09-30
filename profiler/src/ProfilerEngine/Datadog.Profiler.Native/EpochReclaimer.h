// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <cstddef>
#include <cstdint>
#include <mutex>
#include <vector>

// EpochReclaimer
// ==============
//
// Deferred memory reclamation for lock-free readers that may run inside a POSIX
// signal handler (see ManagedCodeCache / CodeRangeTrie).
//
// Readers never block, never allocate and never call into the libc: entering and
// leaving a read-side critical section is one atomic load and two atomic RMWs on a
// reader counter. Writers (never in a signal handler) retire the memory they
// unpublish and the memory is freed only once no reader can still hold a pointer to
// it. Writers never wait for readers either: if readers are still active, the
// retired memory is kept and freed later (next write, or TryReclaimIfPending
// called from a non-signal path such as ManagedCodeCache::GetFunctionInfo).
//
// Algorithm (two-parity epochs)
// -----------------------------
// - _epoch is a monotonically increasing counter. Its parity selects one of two
//   reader counters.
// - A reader loads the epoch E and increments the reader counter of parity E & 1
//   for the duration of its critical section. The counters are sharded (see
//   ReadGuard::CurrentShard) so that concurrent readers do not contend on the same
//   cache line; "_readers[p]" below means the sum of the counters of parity p.
// - A pointer unpublished and retired while the epoch is E is stored in the retired
//   list of parity E & 1.
// - The writer can advance the epoch from E to E + 1 only when
//   _readers[(E + 1) & 1] == 0, i.e. when all the readers that entered during
//   epoch E - 1 (same parity as E + 1) have left. When it does, the list of parity
//   (E + 1) & 1 (items retired during E - 1) is freed.
// => An item retired during epoch E is freed at the advance to E + 2, after both
//    reader counters have been observed at zero *after* it was unpublished. Every
//    reader that could have loaded the pointer (i.e. loaded it before it was
//    unpublished) has left by then. A reader that loaded a stale epoch and got
//    delayed before incrementing its counter can only increment it after the
//    writer observed zero: all its loads then happen after the unpublish (seq_cst
//    total order), so it cannot see the retired pointer.
//
// The pointer publications that readers depend on (see CodeRangeTrie) and the
// reader counter increments are seq_cst, which is what makes the argument above
// hold (both sides are part of the single total order S).
//
// Signal safety
// -------------
// The read side only uses lock-free atomics (checked with static_asserts). A signal
// handler can interrupt a writer (even on the same thread) at any point without
// deadlocking because readers never take _retireMutex.
class EpochReclaimer
{
public:
    using Deleter = void (*)(void*);

    class ReadGuard
    {
    public:
        explicit ReadGuard(const EpochReclaimer& reclaimer) noexcept
        {
            auto epoch = reclaimer._epoch.load(std::memory_order_seq_cst);
            _counter = &reclaimer._shards[CurrentShard()].Count[epoch & 1];
            _counter->fetch_add(1, std::memory_order_seq_cst);
        }

        ~ReadGuard()
        {
            // release: every read done under this guard happens-before the writer
            // observing the counter back at zero (and freeing the memory).
            _counter->fetch_sub(1, std::memory_order_release);
        }

        ReadGuard(const ReadGuard&) = delete;
        ReadGuard& operator=(const ReadGuard&) = delete;
        ReadGuard(ReadGuard&&) = delete;
        ReadGuard& operator=(ReadGuard&&) = delete;

    private:
        // Spread the threads over the shards to avoid contention on a single cache
        // line: the stack of each thread lives in a different part of the address
        // space (hashed at a 4KB granularity). A thread may use different shards over
        // time: the guard keeps the counter it incremented.
        static std::size_t CurrentShard() noexcept
        {
            char marker;
            auto stackAddress = static_cast<std::uint64_t>(reinterpret_cast<std::uintptr_t>(&marker));
            return static_cast<std::size_t>(((stackAddress >> 12) * 0x9E3779B97F4A7C15ull) >> (64 - ShardBits));
        }

        std::atomic<int32_t>* _counter;
    };

    EpochReclaimer() = default;
    ~EpochReclaimer();

    EpochReclaimer(const EpochReclaimer&) = delete;
    EpochReclaimer& operator=(const EpochReclaimer&) = delete;

    // Writer side (not signal-safe). The pointer must already be unpublished.
    void Retire(void* p, Deleter deleter, std::size_t size);

    // Writer side (not signal-safe). Frees whatever can be freed without waiting.
    void TryReclaim();

    // Not signal-safe. Cheap when there is nothing to reclaim (one relaxed load),
    // so it can be called on a frequent path.
    void TryReclaimIfPending()
    {
        if (_pendingBytes.load(std::memory_order_relaxed) != 0)
        {
            TryReclaim();
        }
    }

    // Bytes retired but not freed yet (for memory accounting in tests/benchmarks).
    std::size_t PendingBytes() const noexcept
    {
        return _pendingBytes.load(std::memory_order_relaxed);
    }

private:
    struct Retired
    {
        void* Pointer;
        Deleter Delete;
        std::size_t Size;
    };

    static constexpr unsigned ShardBits = 5;
    static constexpr std::size_t ShardCount = std::size_t{1} << ShardBits;

    // Reader counters of one shard, for each epoch parity, on their own cache line
    struct alignas(64) Shard
    {
        std::atomic<int32_t> Count[2]{};
    };

    bool HasReaders(uint32_t parity) const noexcept;

    void FreeList(std::vector<Retired>& list);

    alignas(64) std::atomic<uint32_t> _epoch{0};
    mutable Shard _shards[ShardCount];

    std::mutex _retireMutex;
    std::vector<Retired> _retired[2];
    std::atomic<std::size_t> _pendingBytes{0};

    static_assert(std::atomic<uint32_t>::is_always_lock_free, "the read side must be lock-free to be signal-safe");
    static_assert(std::atomic<int32_t>::is_always_lock_free, "the read side must be lock-free to be signal-safe");
};
