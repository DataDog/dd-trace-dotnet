// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

// Benchmarks: legacy ManagedCodeCache data structure (paged map + RW locks) vs the
// lock-free CodeRangeTrie based ManagedCodeCache.
// See profiler/docs/ManagedCodeCache.md for the results and how to run them.

#include "HeapCounter.h"
#include "LegacyCodeCache.h"
#include "ManagedCodeCache.h"
#include "Workload.h"

#include <benchmark/benchmark.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <map>
#include <memory>
#include <mutex>
#include <thread>
#include <vector>

namespace {

// Adapters exposing the same API for both implementations
struct Legacy
{
    legacy::LegacyCodeCache Cache;

    void AddFunction(const CodeRange& range)
    {
        Cache.AddFunctionRanges({range});
    }

    void AddModules(const std::vector<ModuleCodeRange>& ranges)
    {
        Cache.AddModuleRanges(ranges);
    }

    void RemoveModules(const std::vector<ModuleCodeRange>& ranges)
    {
        Cache.RemoveModuleRanges(ranges);
    }

    std::optional<bool> IsManaged(std::uintptr_t ip) const
    {
        return Cache.IsManaged(ip);
    }

    bool GetFunctionInfo(std::uintptr_t ip)
    {
        return Cache.GetFunctionInfo(ip).has_value();
    }

    std::size_t MemoryUsage() const
    {
        return Cache.MemoryUsage();
    }

    std::size_t WalkStack(const std::uintptr_t* ips, std::size_t count) const
    {
        std::size_t managed = 0;
        for (std::size_t i = 0; i < count; i++)
        {
            managed += Cache.IsManaged(ips[i]).value_or(false);
        }
        return managed;
    }
};

struct Trie
{
    ManagedCodeCache Cache{nullptr};

    void AddFunction(const CodeRange& range)
    {
        Cache.AddFunctionRangesToCache({range});
    }

    void AddModules(const std::vector<ModuleCodeRange>& ranges)
    {
        Cache.AddModuleRangesToCache(ranges);
    }

    void RemoveModules(const std::vector<ModuleCodeRange>& ranges)
    {
        Cache.RemoveModuleRangesFromCache(ranges);
    }

    std::optional<bool> IsManaged(std::uintptr_t ip) const
    {
        return Cache.IsManaged(ip);
    }

    // Only called with JIT hits: never reaches the ICorProfilerInfo fallback
    bool GetFunctionInfo(std::uintptr_t ip)
    {
        return Cache.GetFunctionInfo(ip).has_value();
    }

    std::size_t MemoryUsage() const
    {
        return Cache.MemoryUsageForTest();
    }

    // Same as the HybridUnwinder: one read scope for the whole stack walk (when
    // available: template so that older versions of the cache still compile)
    template <typename C = ManagedCodeCache>
    std::size_t WalkStack(const std::uintptr_t* ips, std::size_t count) const
    {
        const C& cache = Cache;
        std::size_t managed = 0;
        if constexpr (requires { cache.EnterReadScope(); })
        {
            auto scope = cache.EnterReadScope();
            for (std::size_t i = 0; i < count; i++)
            {
                managed += cache.IsManaged(ips[i], scope).value_or(false);
            }
        }
        else
        {
            for (std::size_t i = 0; i < count; i++)
            {
                managed += cache.IsManaged(ips[i]).value_or(false);
            }
        }
        return managed;
    }
};

const bench::Workload& GetWorkload(std::size_t nbFunctions)
{
    static std::mutex m;
    static std::map<std::size_t, std::unique_ptr<bench::Workload>> workloads;
    std::lock_guard<std::mutex> lock(m);
    auto& w = workloads[nbFunctions];
    if (w == nullptr)
    {
        w = std::make_unique<bench::Workload>(bench::MakeWorkload(nbFunctions));
    }
    return *w;
}

template <typename Impl>
Impl& GetPopulatedCache(std::size_t nbFunctions)
{
    static std::mutex m;
    static std::map<std::size_t, std::unique_ptr<Impl>> caches;
    std::lock_guard<std::mutex> lock(m);
    auto& c = caches[nbFunctions];
    if (c == nullptr)
    {
        c = std::make_unique<Impl>();
        auto const& w = GetWorkload(nbFunctions);
        for (auto const& f : w.Functions)
        {
            c->AddFunction(f);
        }
        c->AddModules(w.Modules);
    }
    return *c;
}

enum class Query
{
    JitHit,
    R2RHit,
    NativeMiss,
    Mixed
};

const std::vector<std::uintptr_t>& GetQueries(const bench::Workload& w, Query q)
{
    switch (q)
    {
        case Query::JitHit:
            return w.JitHits;
        case Query::R2RHit:
            return w.R2RHits;
        case Query::NativeMiss:
            return w.NativeMiss;
        default:
            return w.Mixed;
    }
}

//
// 1. Single/multi-threaded read latency (no writer)
//
template <typename Impl, Query Q>
void BM_IsManaged(benchmark::State& state)
{
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto const& cache = GetPopulatedCache<Impl>(nbFunctions);
    auto const& queries = GetQueries(GetWorkload(nbFunctions), Q);

    std::size_t i = state.thread_index() * 997;
    std::size_t failures = 0;
    for (auto _ : state)
    {
        auto result = cache.IsManaged(queries[i++ & (bench::QueryCount - 1)]);
        failures += !result.has_value();
        benchmark::DoNotOptimize(result);
    }
    state.counters["nullopt"] = benchmark::Counter(static_cast<double>(failures), benchmark::Counter::kAvgIterations);
}

// A stack walk: 32 frames looked up one after the other (per frame time reported)
template <typename Impl>
void BM_StackWalk(benchmark::State& state)
{
    constexpr std::size_t Depth = 32;
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto const& cache = GetPopulatedCache<Impl>(nbFunctions);
    auto const& queries = GetWorkload(nbFunctions).Mixed;

    std::size_t i = (state.thread_index() * 997) & (bench::QueryCount - 1);
    for (auto _ : state)
    {
        benchmark::DoNotOptimize(cache.WalkStack(&queries[i], Depth));
        i = (i + Depth) & (bench::QueryCount - 1);
    }
    state.SetItemsProcessed(static_cast<int64_t>(state.iterations() * Depth));
    state.counters["ns_per_frame"] = benchmark::Counter(static_cast<double>(state.iterations() * Depth),
        benchmark::Counter::kIsRate | benchmark::Counter::kInvert);
}

template <typename Impl>
void BM_GetFunctionInfo(benchmark::State& state)
{
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto& cache = GetPopulatedCache<Impl>(nbFunctions);
    auto const& queries = GetWorkload(nbFunctions).JitHits;

    std::size_t i = state.thread_index() * 997;
    for (auto _ : state)
    {
        benchmark::DoNotOptimize(cache.GetFunctionInfo(queries[i++ & (bench::QueryCount - 1)]));
    }
}

//
// 2. Read latency while a writer is continuously JIT-compiling new methods
//    (the scenario the lock-free design is for). Half of the lookups hit the
//    most recently compiled methods (on the pages being written), half are a
//    regular stack-walk-like mix.
//    range(1) is the writer rate in methods/s (0 = as fast as possible). A fixed
//    rate gives the same write pressure to both implementations.
//
template <typename Impl>
void BM_IsManaged_UnderWriter(benchmark::State& state)
{
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto const& w = GetWorkload(nbFunctions);

    // Fresh cache (the writer grows it)
    auto cache = std::make_unique<Impl>();
    for (auto const& f : w.Functions)
    {
        cache->AddFunction(f);
    }
    cache->AddModules(w.Modules);

    std::atomic<bool> stop{false};
    std::atomic<std::uintptr_t> lastWritten{w.Functions.back().startAddress};
    std::atomic<std::uint64_t> writes{0};

    auto rate = state.range(1);
    std::thread writer([&, rate]() {
        std::uintptr_t address = w.NextFreeAddress + 0x100000;
        std::uintptr_t functionId = 0x80000000;
        auto period = rate == 0 ? std::chrono::nanoseconds(0) : std::chrono::nanoseconds(1000000000 / rate);
        auto next = std::chrono::steady_clock::now();
        while (!stop.load(std::memory_order_relaxed))
        {
            if (rate != 0)
            {
                next += period;
                while (std::chrono::steady_clock::now() < next && !stop.load(std::memory_order_relaxed))
                {
                }
            }
            // 128-byte methods: a new 64KB page every 512 methods
            cache->AddFunction({address, address + 127, functionId++, false});
            lastWritten.store(address, std::memory_order_relaxed);
            address += 128;
            writes.fetch_add(1, std::memory_order_relaxed);
        }
    });

    std::vector<std::uint32_t> latencies;
    latencies.reserve(1 << 22);
    std::size_t i = 0;
    std::size_t failures = 0;
    for (auto _ : state)
    {
        std::uintptr_t ip = (i & 1) ? w.Mixed[i & (bench::QueryCount - 1)]
                                    : lastWritten.load(std::memory_order_relaxed) - 128 * ((i >> 1) & 63);
        i++;

        auto start = std::chrono::steady_clock::now();
        auto result = cache->IsManaged(ip);
        auto end = std::chrono::steady_clock::now();

        failures += !result.has_value();
        benchmark::DoNotOptimize(result);
        if (latencies.size() < latencies.capacity())
        {
            latencies.push_back(static_cast<std::uint32_t>(std::chrono::duration_cast<std::chrono::nanoseconds>(end - start).count()));
        }
    }

    stop = true;
    writer.join();

    std::sort(latencies.begin(), latencies.end());
    auto percentile = [&](double p) {
        return latencies.empty() ? 0.0 : static_cast<double>(latencies[static_cast<std::size_t>(p * (latencies.size() - 1))]);
    };
    state.counters["p50_ns"] = percentile(0.50);
    state.counters["p99_ns"] = percentile(0.99);
    state.counters["p999_ns"] = percentile(0.999);
    state.counters["max_ns"] = latencies.empty() ? 0.0 : latencies.back();
    state.counters["nullopt_pct"] = 100.0 * static_cast<double>(failures) / static_cast<double>(std::max<std::size_t>(i, 1));
    state.counters["writes"] = benchmark::Counter(static_cast<double>(writes.load()), benchmark::Counter::kIsRate);
}

//
// 3. Write throughput and memory
//
template <typename Impl, bool Shuffled>
void BM_AddFunction(benchmark::State& state)
{
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto functions = GetWorkload(nbFunctions).Functions;
    if (Shuffled)
    {
        std::mt19937_64 rng(7);
        std::shuffle(functions.begin(), functions.end(), rng);
    }

    std::size_t memory = 0;
    for (auto _ : state)
    {
        auto cache = std::make_unique<Impl>();
        for (auto const& f : functions)
        {
            cache->AddFunction(f);
        }

        state.PauseTiming();
        memory = cache->MemoryUsage();
        cache.reset();
        state.ResumeTiming();
    }

    state.SetItemsProcessed(static_cast<int64_t>(state.iterations() * nbFunctions));
    state.counters["bytes"] = static_cast<double>(memory);
    state.counters["bytes_per_fn"] = static_cast<double>(memory) / nbFunctions;
}

template <typename Impl>
void BM_AddRemoveModules(benchmark::State& state)
{
    auto const& w = GetWorkload(1000);
    auto cache = std::make_unique<Impl>();
    cache->AddModules(w.Modules);

    // Load/unload one extra module (e.g. a collectible assembly)
    std::vector<ModuleCodeRange> extra{{bench::ModulesBase - 0x1000000, bench::ModulesBase - 0x800001}};
    for (auto _ : state)
    {
        cache->AddModules(extra);
        cache->RemoveModules(extra);
    }
}

//
// 4. Memory: heap bytes really in use (counted by the replaced operator new) by a
//    cache populated with the workload (JIT ranges + R2R modules), including the
//    object itself. range(1) == 1 inserts the methods in a random order.
//
template <typename Impl>
void BM_Memory(benchmark::State& state)
{
    auto nbFunctions = static_cast<std::size_t>(state.range(0));
    auto functions = GetWorkload(nbFunctions).Functions;
    if (state.range(1) != 0)
    {
        std::mt19937_64 rng(7);
        std::shuffle(functions.begin(), functions.end(), rng);
    }
    auto const& modules = GetWorkload(nbFunctions).Modules;

    std::int64_t bytes = 0;
    for (auto _ : state)
    {
        auto before = bench::LiveHeapBytes();
        auto cache = std::make_unique<Impl>();
        for (auto const& f : functions)
        {
            cache->AddFunction(f);
        }
        cache->AddModules(modules);
        bytes = bench::LiveHeapBytes() - before;
    }

    state.counters["heap_bytes"] = static_cast<double>(bytes);
    state.counters["bytes_per_fn"] = static_cast<double>(bytes) / nbFunctions;
}

constexpr int64_t Small = 1000;
constexpr int64_t Medium = 10000;
constexpr int64_t Large = 100000;

} // namespace

#define SIZES Arg(Small)->Arg(Medium)->Arg(Large)

// Reads, single thread
BENCHMARK(BM_IsManaged<Legacy, Query::JitHit>)->SIZES;
BENCHMARK(BM_IsManaged<Trie, Query::JitHit>)->SIZES;
BENCHMARK(BM_IsManaged<Legacy, Query::R2RHit>)->SIZES;
BENCHMARK(BM_IsManaged<Trie, Query::R2RHit>)->SIZES;
BENCHMARK(BM_IsManaged<Legacy, Query::NativeMiss>)->SIZES;
BENCHMARK(BM_IsManaged<Trie, Query::NativeMiss>)->SIZES;
BENCHMARK(BM_IsManaged<Legacy, Query::Mixed>)->SIZES;
BENCHMARK(BM_IsManaged<Trie, Query::Mixed>)->SIZES;
BENCHMARK(BM_GetFunctionInfo<Legacy>)->SIZES;
BENCHMARK(BM_GetFunctionInfo<Trie>)->SIZES;

// Stack walks (32 frames), single and multi-threaded
BENCHMARK(BM_StackWalk<Legacy>)->SIZES;
BENCHMARK(BM_StackWalk<Trie>)->SIZES;
BENCHMARK(BM_StackWalk<Legacy>)->Arg(Medium)->ThreadRange(2, 16)->UseRealTime();
BENCHMARK(BM_StackWalk<Trie>)->Arg(Medium)->ThreadRange(2, 16)->UseRealTime();

// Reads, multi-threaded (no writer)
BENCHMARK(BM_IsManaged<Legacy, Query::Mixed>)->Arg(Medium)->ThreadRange(1, 16)->UseRealTime();
BENCHMARK(BM_IsManaged<Trie, Query::Mixed>)->Arg(Medium)->ThreadRange(1, 16)->UseRealTime();

// Reads with a concurrent writer
BENCHMARK(BM_IsManaged_UnderWriter<Legacy>)->Args({Medium, 100000})->Args({Medium, 0})->Iterations(2000000)->UseRealTime();
BENCHMARK(BM_IsManaged_UnderWriter<Trie>)->Args({Medium, 100000})->Args({Medium, 0})->Iterations(2000000)->UseRealTime();

// Writes
BENCHMARK(BM_AddFunction<Legacy, false>)->SIZES->Unit(benchmark::kMillisecond);
BENCHMARK(BM_AddFunction<Trie, false>)->SIZES->Unit(benchmark::kMillisecond);
BENCHMARK(BM_AddFunction<Legacy, true>)->SIZES->Unit(benchmark::kMillisecond);
BENCHMARK(BM_AddFunction<Trie, true>)->SIZES->Unit(benchmark::kMillisecond);
BENCHMARK(BM_Memory<Legacy>)->ArgsProduct({{Small, Medium, Large}, {0, 1}})->Iterations(1);
BENCHMARK(BM_Memory<Trie>)->ArgsProduct({{Small, Medium, Large}, {0, 1}})->Iterations(1);

BENCHMARK(BM_AddRemoveModules<Legacy>);
BENCHMARK(BM_AddRemoveModules<Trie>);

BENCHMARK_MAIN();
