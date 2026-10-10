// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <memory>
#include <mutex>
#include <optional>
#include <vector>

#include "cor.h"
#include "corprof.h"

#include "CodeRangeTrie.h"
#include "EpochReclaimer.h"

class IConfiguration;

// Thread-safe cache for managed code address ranges
// 
// STRATEGY: Event-Driven Accumulative Caching
// ===========================================
// Based on .NET runtime analysis, we discovered:
// 1. GetCodeInfo3 only returns ONE native code version (the first one)
// 2. JITCompilationFinished is called MULTIPLE times for tiered compilation
// 3. Runtime keeps ALL native code versions in memory (for stack safety)
// 4. Therefore: We capture each tier when it's compiled and NEVER remove old ranges
//
// This approach:
// - Works with ICorProfilerInfo4 (.NET Framework 4.5+)
// - Captures all tiers (Tier 0, Tier 1, Tier 1 OSR, etc.)
// - Has minimal overhead (one API call per tier)
// - Is simpler than querying with Info9
//
// See: dotnet-runtime/docs/design/features/code-versioning-profiler-breaking-changes.md
//
// CONCURRENCY: lock-free readers
// ==============================
// IsManaged is called from the profiler signal handlers (stack walking). The code
// ranges are stored in a CodeRangeTrie and the R2R module ranges in a ModuleRangeSet:
// readers never take a lock (so they can neither block nor fail because a writer is
// active), writers are serialized and retire the memory they replace to the
// EpochReclaimer. See profiler/docs/ManagedCodeCache.md.
class ManagedCodeCache {
public:
    static constexpr FunctionID InvalidFunctionId = -1;

    struct FunctionInfo {
        FunctionID FunctionId;
        bool IsDynamic;
    };

    // Read-side critical section (signal-safe). Entering/leaving one costs two
    // atomic increments: callers doing several lookups in a row (e.g. a stack walk)
    // should hold one ReadScope for all of them instead of paying that cost for each
    // IsManaged call. It only delays the reclamation of replaced memory: keep it short
    // (no blocking call) and never jump over its destructor (siglongjmp), which would
    // prevent memory from ever being reclaimed.
    class ReadScope
    {
    public:
        ReadScope(const ReadScope&) = delete;
        ReadScope& operator=(const ReadScope&) = delete;

    private:
        friend class ManagedCodeCache;
        explicit ReadScope(const EpochReclaimer& reclaimer) noexcept :
            _guard(reclaimer)
        {
        }

        EpochReclaimer::ReadGuard _guard;
    };

    explicit ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo);
    ~ManagedCodeCache();

    [[nodiscard]] ReadScope EnterReadScope() const noexcept
    {
        return ReadScope(_reclaimer);
    }

    // Signal-safe lookup (lock-free, no allocation). The lookup never fails: the
    // std::optional is kept so callers stay agnostic of the implementation.
    [[nodiscard]] std::optional<bool> IsManaged(std::uintptr_t ip) const noexcept;
    [[nodiscard]] std::optional<bool> IsManaged(std::uintptr_t ip, const ReadScope& scope) const noexcept;

    // Not signal-safe (may call ICorProfilerInfo::GetFunctionFromIP)
    [[nodiscard]] std::optional<FunctionInfo> GetFunctionInfo(std::uintptr_t ip) noexcept;

    // isDynamic is true only when called from DynamicMethodJITCompilationFinished
    void AddFunction(FunctionID functionId, bool isDynamic);
    void AddModule(ModuleID moduleId);
    void RemoveModule(ModuleID moduleId);

    bool Initialize();

private:
    // Query the runtime for code ranges for a specific version
    // This is called when a new tier is compiled
    std::vector<CodeRange> GetCodeRanges(FunctionID functionId, bool isDynamic);
    std::vector<ModuleCodeRange> GetModuleCodeRanges(ModuleID moduleId);

// Expose the helpers below to tests and benchmarks without duplicating the declarations.
#ifdef DD_TEST
public:
#endif
    // Append new ranges to the cache (accumulative - never removes old ranges)
    // This preserves old tier code that might still be on the stack
    void AddFunctionRangesToCache(std::vector<CodeRange> newRanges);
    void AddModuleRangesToCache(std::vector<ModuleCodeRange> moduleCodeRanges);
    void RemoveModuleRangesFromCache(std::vector<ModuleCodeRange> moduleCodeRanges);
#ifdef DD_TEST
    // Test-only hooks: hold the writer locks to check that readers are never blocked.
    std::unique_lock<std::mutex> LockCodeRangesWriterForTest()
    {
        return _codeRanges.LockWriterForTest();
    }
    std::unique_lock<std::mutex> LockModulesWriterForTest()
    {
        return _modules.LockWriterForTest();
    }
    std::size_t PendingReclaimBytesForTest() const
    {
        return _reclaimer.PendingBytes();
    }
    std::size_t MemoryUsageForTest() const
    {
        return _codeRanges.MemoryUsage() + _modules.MemoryUsage() + _reclaimer.PendingBytes();
    }
private:
#endif
    std::optional<FunctionInfo> GetFunctionInfoImpl(std::uintptr_t ip) const noexcept;
    bool IsCodeInR2RModule(std::uintptr_t ip) const noexcept;
    std::optional<FunctionID> GetFunctionFromIP_Original(std::uintptr_t ip) noexcept;
    void AddFunctionImpl(FunctionID functionId, bool isDynamic);

    // Must be declared first: the trie and the module set retire memory to it.
    EpochReclaimer _reclaimer;
    CodeRangeTrie _codeRanges;
    ModuleRangeSet _modules;

    // Profiler interface (ICorProfilerInfo4 is available in .NET Framework 4.5+)
    ICorProfilerInfo4* _profilerInfo;
};
