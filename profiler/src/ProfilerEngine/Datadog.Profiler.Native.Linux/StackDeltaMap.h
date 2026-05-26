// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "StackDeltaTypes.h"

#include <algorithm>
#include <cstddef>
#include <cstdint>
#include <vector>

// Per-module delta info: address range + sorted delta array.
struct ModuleDeltaInfo
{
    std::uintptr_t loadAddr;
    std::uintptr_t endAddr;
    // Deltas sorted by address. Intervals are [deltas[i].address, deltas[i+1].address).
    std::vector<StackDelta> deltas;
};

// StackDeltaMap stores pre-computed unwind information for all loaded native
// modules. It is built on a background thread and read (without locks) from
// the signal handler via an atomic pointer swap.
//
// Lookup is two binary searches:
//   1. Find the module containing the PC (sorted by loadAddr)
//   2. Find the interval containing the PC within that module
//
// All data is immutable after construction, so reads are signal-safe.
class StackDeltaMap
{
public:
    StackDeltaMap() = default;
    ~StackDeltaMap() = default;

    StackDeltaMap(const StackDeltaMap&) = delete;
    StackDeltaMap& operator=(const StackDeltaMap&) = delete;
    StackDeltaMap(StackDeltaMap&&) = default;
    StackDeltaMap& operator=(StackDeltaMap&&) = default;

    // Add a module's deltas. Must be called before Finalize().
    void AddModule(std::uintptr_t loadAddr, std::uintptr_t endAddr,
                   std::vector<StackDelta> deltas)
    {
        _modules.push_back({loadAddr, endAddr, std::move(deltas)});
    }

    // Sort modules by loadAddr. Must be called after all AddModule() calls
    // and before any Lookup() calls.
    void Finalize()
    {
        std::sort(_modules.begin(), _modules.end(),
                  [](const ModuleDeltaInfo& a, const ModuleDeltaInfo& b)
                  { return a.loadAddr < b.loadAddr; });
    }

    // Look up the unwind info for a given PC.
    // Returns nullptr if the PC is not in any known module or interval.
    // Signal-safe: no allocation, no locks, pure binary search over
    // immutable data.
    const UnwindInfo* Lookup(std::uintptr_t pc) const
    {
        if (_modules.empty())
            return nullptr;

        // Binary search for the module containing this PC
        auto modIt = std::upper_bound(
            _modules.begin(), _modules.end(), pc,
            [](std::uintptr_t addr, const ModuleDeltaInfo& m) { return addr < m.loadAddr; });

        if (modIt == _modules.begin())
            return nullptr;

        --modIt;
        if (pc >= modIt->endAddr)
            return nullptr;

        const auto& deltas = modIt->deltas;
        if (deltas.empty())
            return nullptr;

        // Binary search for the interval containing this PC
        auto deltaIt = std::upper_bound(
            deltas.begin(), deltas.end(), pc,
            [](std::uintptr_t addr, const StackDelta& d) { return addr < d.address; });

        if (deltaIt == deltas.begin())
            return nullptr;

        --deltaIt;
        return &deltaIt->info;
    }

    std::size_t ModuleCount() const { return _modules.size(); }
    std::size_t TotalDeltas() const
    {
        std::size_t count = 0;
        for (const auto& m : _modules)
            count += m.deltas.size();
        return count;
    }

    bool IsEmpty() const { return _modules.empty(); }

#ifdef DD_TEST
    const std::vector<ModuleDeltaInfo>& GetModules() const { return _modules; }
#endif

private:
    std::vector<ModuleDeltaInfo> _modules;
};
