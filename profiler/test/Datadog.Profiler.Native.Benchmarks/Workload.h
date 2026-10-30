// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// Deterministic synthetic workload mimicking the address layout seen by
// ManagedCodeCache in a .NET process:
// - JIT-compiled methods are allocated one after the other in a few code heaps
//   (reserved regions, 256MB apart), with sizes mostly between 64B and 1KB and a
//   long tail up to 16KB. Some methods are re-jitted later (tier-1), which appends
//   new ranges at the current allocation pointer.
// - R2R modules (framework + application assemblies) have 1 to 20MB of executable
//   sections, mapped in another area.
// - Native code (libcoreclr, libc, the profiler...) lives outside of both.

#include "CodeRangeTrie.h"

#include <algorithm>
#include <cmath>
#include <cstdint>
#include <random>
#include <vector>

namespace bench {

constexpr std::uintptr_t CodeHeapBase = 0x7F1200000000;
constexpr std::uintptr_t CodeHeapStride = 0x10000000; // 256MB between code heaps
constexpr std::uintptr_t CodeHeapSize = 0x01000000;   // 16MB used per code heap
constexpr std::uintptr_t ModulesBase = 0x7F3000000000;
constexpr std::uintptr_t NativeBase = 0x7F5500000000;

constexpr std::size_t QueryCount = 1 << 14; // power of 2

struct Workload
{
    std::vector<CodeRange> Functions;       // in JIT order (monotonic per code heap)
    std::vector<ModuleCodeRange> Modules;
    std::vector<std::uintptr_t> JitHits;    // IPs inside JIT-compiled methods
    std::vector<std::uintptr_t> R2RHits;    // IPs inside R2R modules
    std::vector<std::uintptr_t> NativeMiss; // IPs in native code
    std::vector<std::uintptr_t> Mixed;      // 70% JIT, 20% R2R, 10% native (stack walk like)
    std::uintptr_t NextFreeAddress;         // first address after the last JIT-compiled method
};

inline std::uintptr_t DrawMethodSize(std::mt19937_64& rng)
{
    // log-uniform between 32B and 16KB, skewed toward small methods
    std::uniform_real_distribution<double> u(0.0, 1.0);
    double x = u(rng);
    double bits = 5 + 9 * x * x; // 2^5 .. 2^14
    auto size = static_cast<std::uintptr_t>(std::pow(2.0, bits));
    return std::max<std::uintptr_t>(size & ~std::uintptr_t{0xF}, 16);
}

inline Workload MakeWorkload(std::size_t nbFunctions, std::size_t nbModules = 50, std::uint64_t seed = 42)
{
    Workload w;
    std::mt19937_64 rng(seed);

    // JIT-compiled code: sequential allocation in 16MB chunks of 256MB-apart code heaps
    w.Functions.reserve(nbFunctions);
    std::uintptr_t heap = CodeHeapBase;
    std::uintptr_t current = heap;
    std::uniform_int_distribution<int> gapDist(0, 3);
    std::uniform_int_distribution<int> percent(0, 99);
    for (std::size_t i = 0; i < nbFunctions; i++)
    {
        auto size = DrawMethodSize(rng);
        if (current + size >= heap + CodeHeapSize)
        {
            heap += CodeHeapStride;
            current = heap;
        }

        // ~20% of the methods are "re-jitted" (same function id as an earlier method)
        std::uintptr_t functionId = (i > 0 && percent(rng) < 20) ? w.Functions[rng() % i].functionId : 0x100000 + i;
        w.Functions.push_back({current, current + size - 1, functionId, percent(rng) < 5});
        current += size + 16 * gapDist(rng); // code heap alignment/padding
    }
    w.NextFreeAddress = current;

    // R2R modules
    std::uintptr_t moduleAddress = ModulesBase;
    std::uniform_int_distribution<std::uintptr_t> moduleSize(1 << 20, 20 << 20);
    for (std::size_t i = 0; i < nbModules; i++)
    {
        auto size = moduleSize(rng) & ~std::uintptr_t{0xFFF};
        w.Modules.push_back({moduleAddress, moduleAddress + size - 1});
        moduleAddress += size + (1 << 20);
    }
    std::shuffle(w.Modules.begin(), w.Modules.end(), rng); // load order != address order

    // Queries
    auto jitHit = [&]() {
        auto const& f = w.Functions[rng() % w.Functions.size()];
        return f.startAddress + rng() % (f.endAddress - f.startAddress + 1);
    };
    auto r2rHit = [&]() {
        auto const& m = w.Modules[rng() % w.Modules.size()];
        return m.startAddress + rng() % (m.endAddress - m.startAddress + 1);
    };
    auto nativeMiss = [&]() { return NativeBase + rng() % (64 << 20); };

    for (std::size_t i = 0; i < QueryCount; i++)
    {
        w.JitHits.push_back(jitHit());
        w.R2RHits.push_back(r2rHit());
        w.NativeMiss.push_back(nativeMiss());
        auto p = percent(rng);
        w.Mixed.push_back(p < 70 ? jitHit() : (p < 90 ? r2rHit() : nativeMiss()));
    }

    return w;
}

} // namespace bench
