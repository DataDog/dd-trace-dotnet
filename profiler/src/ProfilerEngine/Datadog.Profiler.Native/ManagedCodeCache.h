#pragma once
#include "ServiceBase.h"
#include "AutoResetEvent.h"

#include <atomic>
#include <future>
#include <vector>
#include <unordered_map>
#include <unordered_set>
#include <optional>
#include <shared_mutex>
#include <thread>
#include <mutex>
#include <set>
#include <algorithm>


#include "cor.h"
#include "corprof.h"

// Managed code information for a specific address range
struct ManagedCodeInfo {
    FunctionID functionId;
    
    // Future: Add unwind info here
    // UnwindInfo unwindInfo;
};

// Represents a single contiguous code range
struct CodeRange {
    UINT_PTR startAddress;
    UINT_PTR endAddress;  // Exclusive
    FunctionID functionId;
    
    // For binary search
    bool operator<(const CodeRange& other) const {
        return startAddress < other.startAddress;
    }
    
    // Check if IP is within this range
    bool contains(UINT_PTR ip) const {
        return ip >= startAddress && ip < endAddress;
    }
};


struct ModuleCodeRange {
    UINT_PTR startAddress;
    UINT_PTR endAddress;
    bool isRemoved = false;
    // For binary search
    bool operator<(const ModuleCodeRange& other) const {
        return startAddress < other.startAddress;
    }
    
    // Check if IP is within this range
    bool contains(std::uintptr_t ip) const {
        return ip >= startAddress && ip < endAddress;
    }
};

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
class ManagedCodeCache : public ServiceBase {
public:
    explicit ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo, IConfiguration* pConfiguration);
    ~ManagedCodeCache();

    // Signal-safe lookup methods (no allocation)
    [[nodiscard]] bool IsManaged(std::uintptr_t ip) const noexcept;

    // Not signal-safe
    [[nodiscard]] std::optional<FunctionID> GetFunctionId(std::uintptr_t ip) noexcept;

    void AddFunction(FunctionID functionId);
    void AddModule(ModuleID moduleId);
    void RemoveModule(ModuleID moduleId);

    bool StartImpl() override;
    bool StopImpl() override;
    const char* GetName() override;

private:
    // Each page has its own data + lock for fine-grained concurrency
    struct PageEntry {
        std::vector<CodeRange> ranges;  // Sorted by startAddress
        mutable std::shared_mutex lock;  // Reader-writer lock
        
        PageEntry() = default;
        
        // Needed for map operations (can't copy mutex)
        PageEntry(PageEntry&& other) noexcept 
            : ranges(std::move(other.ranges)) {}
        
        PageEntry& operator=(PageEntry&& other) noexcept {
            ranges = std::move(other.ranges);
            return *this;
        }
        
        // Delete copy operations
        PageEntry(const PageEntry&) = delete;
        PageEntry& operator=(const PageEntry&) = delete;
    };
    
    // Partition address space into 64KB pages for faster lookup
    static constexpr size_t PAGE_SHIFT = 16;  // 64KB pages (size = 1ULL << 16 = 65536)
    
    // Helper: Get page number for an address
    static uint64_t GetPageNumber(UINT_PTR address) {
        return address >> PAGE_SHIFT;
    }
    
    // Helper: Find range in sorted vector using binary search (signal-safe)
    static const CodeRange* FindRangeInVector(
        const std::vector<CodeRange>& ranges,
        UINT_PTR ip) noexcept;
    
    // Query the runtime for code ranges for a specific version
    // This is called when a new tier is compiled
    std::vector<CodeRange> QueryCodeRanges(FunctionID functionId);
    
    // Append new ranges to the cache (accumulative - never removes old ranges)
    // This preserves old tier code that might still be on the stack
    void AppendRangesToCache(std::vector<CodeRange> newRanges);
    void AppendModuleRangeToCache(std::vector<ModuleCodeRange> moduleCodeRanges);
    void AddModuleCodeRangesAsync(std::vector<ModuleCodeRange> moduleCodeRanges);
    void AddFunctionCodeRangesAsync(std::vector<CodeRange> ranges);
    std::vector<ModuleCodeRange> GetModuleCodeRanges(ModuleID moduleId);

    void WorkerThread(std::promise<void> startPromise);
    
    // Helper: Ensure a page exists in the map
    void EnsurePageExists(uint64_t page);
    
    // Map from page number -> page entry (with its own lock)


    std::unordered_map<uint64_t, PageEntry> m_pageMap;
    std::unordered_set<ModuleID> m_moduleMap;
    std::vector<ModuleCodeRange> m_moduleCodeRanges;
    mutable std::shared_mutex m_moduleMapLock;
    
    // Coarse lock ONLY for modifying the map structure itself
    // (adding/removing pages, not modifying page contents)
    mutable std::shared_mutex m_mapStructureLock;
    
    // Profiler interface (ICorProfilerInfo4 is available in .NET Framework 4.5+)
    ICorProfilerInfo4* _profilerInfo;
    std::thread _worker;
    std::atomic<bool> _requestStop;
    
    struct QueueNode;
    std::atomic<QueueNode*> _workerQueueHead;


    std::unique_ptr<QueueNode> DequeueWorkItem();

    template<typename WorkType>
    void EnqueueWork(WorkType work);
    std::optional<FunctionID> GetCodeInfo(std::uintptr_t ip) const noexcept;
    std::optional<FunctionID> GetFunctionId(std::uintptr_t ip) const noexcept;
    bool IsCodeInR2RModule(std::uintptr_t ip) const noexcept;
    std::optional<FunctionID> GetFunctionIdFromIPOriginal(std::uintptr_t ip) noexcept;
    
    AutoResetEvent _workerQueueEvent;

    bool _useCustomGetFunctionFromIP;
};

// Compile-time checks for signal-safety
static_assert(std::is_trivially_copyable_v<ManagedCodeInfo>,
              "ManagedCodeInfo must be trivially copyable for signal-safe access");
static_assert(std::is_trivially_copyable_v<FunctionID>,
              "FunctionID must be trivially copyable");
