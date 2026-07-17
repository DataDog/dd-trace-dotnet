// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedCodeCache.h"

#include "Configuration.h"
#include "CounterMetric.h"
#include "MetricsRegistry.h"

#include <algorithm>
#include <chrono>
#include <set>
#include <variant>

#ifndef _WINDOWS
#include <pthread.h>
#include <signal.h>
#endif

#include "Log.h"

// Maximum time the signal-handler read path waits to acquire a cache lock
// before giving up and returning std::nullopt.
static constexpr auto SignalLockTimeout = std::chrono::microseconds(500);

namespace {

// RAII guard that blocks the profiler signals (SIGPROF for the timer_create CPU
// profiler, SIGUSR1 for wall-time stack collection) for the duration of a cache
// write. This guarantees the writing thread cannot be interrupted by a profiler
// signal whose handler would try to acquire a reader lock on the same mutex,
// which would otherwise deadlock. The signals are deferred (not dropped) and
// delivered once the previous mask is restored.
class ScopedProfilerSignalBlocker
{
public:
#ifndef _WINDOWS
    ScopedProfilerSignalBlocker()
    {
        sigset_t toBlock;
        sigemptyset(&toBlock);
        sigaddset(&toBlock, SIGPROF);
        sigaddset(&toBlock, SIGUSR1);
        pthread_sigmask(SIG_BLOCK, &toBlock, &_previous);
    }

    ~ScopedProfilerSignalBlocker()
    {
        pthread_sigmask(SIG_SETMASK, &_previous, nullptr);
    }

private:
    sigset_t _previous;
#else
    ScopedProfilerSignalBlocker() = default;
    ~ScopedProfilerSignalBlocker() = default;
#endif

    ScopedProfilerSignalBlocker(const ScopedProfilerSignalBlocker&) = delete;
    ScopedProfilerSignalBlocker& operator=(const ScopedProfilerSignalBlocker&) = delete;
    ScopedProfilerSignalBlocker(ScopedProfilerSignalBlocker&&) = delete;
    ScopedProfilerSignalBlocker& operator=(ScopedProfilerSignalBlocker&&) = delete;
};

} // namespace

template <typename Container, typename Value>
std::optional<typename Container::value_type> FindRange(Container const& container, Value const& value)
{
    auto it = std::lower_bound(container.begin(), container.end(), value,
    [](const typename Container::value_type& range, const Value& value) -> bool {
        return range.startAddress <= value;
    });

    if (it == container.cbegin())
    {
        return std::nullopt;
    }

    --it;
    return it->contains(value) ? std::optional{*it} : std::nullopt;
}

// PE32 and PE64 have different optional headers, which complexify the logic to fetch them
// This struct contains the common fields between the two types of headers
struct IMAGE_NT_HEADERS_GENERIC
{
    DWORD Signature;
    IMAGE_FILE_HEADER FileHeader;
    WORD    Magic;
};

ManagedCodeCache::ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo, MetricsRegistry& metricsRegistry)
    : _profilerInfo(pProfilerInfo),
      _lockFailureMetric(metricsRegistry.GetOrRegister<CounterMetric>("dotnet_managed_code_cache_lock_failures"))
{
}

ManagedCodeCache::~ManagedCodeCache() = default;

bool ManagedCodeCache::Initialize()
{
    Log::Info("ManagedCodeCache initialized successfully");
    return true;
}

std::optional<bool> ManagedCodeCache::IsCodeInR2RModule(std::uintptr_t ip, bool signalSafe) const noexcept
{
    // IsCodeInR2RModule can be called in a signal handler or not.
    // If it's called in a signal handler, we use a time-based acquire so the
    // handler waits a bounded amount of time for a writer on another thread
    // instead of failing immediately (and never blocks indefinitely).
    // If it's called not in a signal handler, we use a plain blocking shared lock.
    auto moduleLock = [](std::shared_timed_mutex& mutex, bool signalSafe) {
        if (signalSafe)
        {
            return std::shared_lock<std::shared_timed_mutex>(mutex, SignalLockTimeout);
        }
        return std::shared_lock<std::shared_timed_mutex>(mutex);
    }(_modulesMutex, signalSafe);

    if (!moduleLock.owns_lock())
    {
        return std::nullopt;
    }

    auto moduleCodeRange = FindRange(_modulesCodeRanges, ip);
    if (!moduleCodeRange.has_value())
    {
        return {false};
    }

    if (moduleCodeRange->isRemoved)
    {
        // No print, can be called in a signal handler
        // LogOnce(Debug, "ManagedCodeCache::IsCodeInR2RModule: Module code range was removed for ip: 0x", std::hex, ip);
        return {false};
    }

    return {moduleCodeRange->contains(ip)};
}

// must not be called in a signal handler (GetFunctionFromIP is not signal-safe)
// nor by a managed thread (is that really a valid constraint?)
std::optional<FunctionID> ManagedCodeCache::GetFunctionId(std::uintptr_t ip) noexcept
{
    auto info = GetFunctionIdImpl(ip);
    if (info.has_value())
    {
        return info;
    }

    // Level 2: Check if the IP is within a module code range
    
    auto isR2r = IsCodeInR2RModule(ip, false);
    // GetFunctionId is NOT signal-safe: we pass signalSafe=false, so
    // IsCodeInR2RModule takes the shared lock unconditionally and always
    // returns an engaged optional. The !has_value() guard below is defence
    // in depth in case IsCodeInR2RModule ever grows a new failure path.
    if (!isR2r.has_value() || !isR2r.value())
    {
        // if it has value `false`, just return InvalidFunctionId
        return std::optional<FunctionID>(InvalidFunctionId);
    }

    auto functionId = GetFunctionFromIP_Original(ip);
    if (functionId.has_value() && functionId.value() != InvalidFunctionId) {
        // We found a function id and we can add it synchronously to our cache.
        AddFunctionImpl(functionId.value());
        return std::optional<FunctionID>(functionId.value());
    }
    // If we arrive here, it means that the call to GetFunctionFromIP_Original possibly crashed.
    // Possible reason: race against the CLR unloading the module containing the function.
    // On Windows, we catch the exception and return nullopt.
    // On Linux, we cannot do anything, we'll never get there.

    return functionId;
}

std::optional<FunctionID> ManagedCodeCache::GetFunctionFromIP_Original(std::uintptr_t ip) noexcept
{
    FunctionID functionId;

    // On Windows, the call to GetFunctionFromIP can crash:
    // We may end up in a situation where the module containing that symbol was just unloaded.
    // For linux, we use the custom GetFunctionFromIP based on the code cache

    // Cannot return while in __try/__except (compilation error)
    // We need a flag to know if an access violation exception was raised.
    bool wasAccessViolationRaised = false;
#ifdef _WINDOWS
    __try
    {
#endif
        if (SUCCEEDED(_profilerInfo->GetFunctionFromIP((LPCBYTE)ip, &functionId)))
        {
            return {functionId};
        }
#ifdef _WINDOWS
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        // we could return a fake function id to display a good'ish callstack shape
        // add a metric ?
        wasAccessViolationRaised = true;
    }
#endif

    if (wasAccessViolationRaised)
    {
        return std::nullopt;
    }
    return std::optional<FunctionID>(InvalidFunctionId);
}

std::optional<FunctionID> ManagedCodeCache::GetFunctionIdImpl(std::uintptr_t ip) const noexcept
{
    uint64_t page = GetPageNumber(static_cast<UINT_PTR>(ip));
    
    // Level 1: Find the page (shared lock on map structure)
    std::shared_lock<std::shared_timed_mutex> mapLock(_pagesMutex);
    auto pageIt = _pagesMap.find(page);
    if (pageIt == _pagesMap.end())
    {
        return std::nullopt;  // No code on this page
    }
    
    // Level 2: Binary search within the page's ranges (shared lock on page)
    std::shared_lock<std::shared_timed_mutex> pageLock(pageIt->second.lock);
    auto range = FindRange(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
    if (range.has_value())
    {
        return range->functionId;
    }
    
    return std::nullopt;
}

// can be called in a signal handler
std::optional<bool> ManagedCodeCache::IsManaged(std::uintptr_t ip) const noexcept
{
    // Best effort to identify an instruction pointer. When called from a signal
    // handler, IsManagedImpl uses a time-based lock acquire (bounded wait) and
    // returns std::nullopt if a lock cannot be acquired within the timeout.
    auto result = IsManagedImpl(ip);
    if (!result.has_value())
    {
        // Lock could not be acquired within the timeout: record it. Incr() is an
        // atomic increment, so this is safe to call from a signal handler.
        _lockFailureMetric->Incr();
    }
    return result;
}

std::optional<bool> ManagedCodeCache::IsManagedImpl(std::uintptr_t ip) const noexcept
{
    uint64_t page = GetPageNumber(static_cast<UINT_PTR>(ip));
    
    {
        // Level 1: Find the page (shared lock on map structure)
        std::shared_lock<std::shared_timed_mutex> mapLock(_pagesMutex, SignalLockTimeout);
        if (!mapLock.owns_lock())
        {
            return std::nullopt;
        }
        auto pageIt = _pagesMap.find(page);
        if (pageIt != _pagesMap.end())
        {
            // Level 2: Binary search within the page's ranges (shared lock on page)
            std::shared_lock<std::shared_timed_mutex> pageLock(pageIt->second.lock, SignalLockTimeout);
            if (!pageLock.owns_lock())
            {
                return std::nullopt;
            }
            auto range = FindRange(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
            if (range.has_value())
            {
                return std::optional{true};
            }
        }
    }

    // Page not found or IP not in any JIT-compiled range: check R2R modules
    return IsCodeInR2RModule(ip, true);
}

void ManagedCodeCache::AddFunction(FunctionID functionId)
{
    AddFunctionImpl(functionId);
}

// Maybe rename this into OnJitCompilation
void ManagedCodeCache::AddFunctionImpl(FunctionID functionId)
{
    auto ranges = GetCodeRanges(functionId);

    if (ranges.empty())
    {
        return;
    }

    // The write is performed synchronously. AddFunctionRangesToCache blocks the
    // profiler signals while it holds the writer locks, so the calling (managed)
    // thread cannot be interrupted into a signal handler that would try to take a
    // reader lock on the same mutex (which would otherwise deadlock).
    AddFunctionRangesToCache(std::move(ranges));
}

// Maybe rename this into OnModuleLoaded
void ManagedCodeCache::AddModule(ModuleID moduleId)
{
    auto moduleCodeRanges = GetModuleCodeRanges(moduleId);

    if (moduleCodeRanges.empty())
    {
        Log::Debug("ManagedCodeCache::AddModule: No module code ranges found for module id: ", moduleId);
        return;
    }

    if (Log::IsDebugEnabled())
    {
        std::stringstream ss;
        for (auto &range : moduleCodeRanges)
        {
            ss << std::hex << "Range: [0x" << range.startAddress << " - 0x" << range.endAddress  << "], " << std::endl;
        }
        Log::Debug("ManagedCodeCache::AddModule: Module code ranges for module id: ", moduleId, " are: ", ss.str());
    }

    // The write is performed synchronously. AddModuleRangesToCache blocks the
    // profiler signals while it holds the writer lock, so the calling (managed)
    // thread cannot be interrupted into a signal handler that would try to take a
    // reader lock on the same mutex (which would otherwise deadlock).
    AddModuleRangesToCache(std::move(moduleCodeRanges));
}

void ManagedCodeCache::RemoveModule(ModuleID moduleId)
{
    auto moduleCodeRanges = GetModuleCodeRanges(moduleId);
    if (moduleCodeRanges.empty())
    {
        return;
    }

    // Block profiler signals while holding the writer lock so this thread cannot
    // be interrupted into a signal handler that would deadlock on the same mutex.
    ScopedProfilerSignalBlocker signalBlocker;
    std::unique_lock<std::shared_timed_mutex> moduleLock(_modulesMutex);
    for (auto const& range : moduleCodeRanges)
    {
        auto it = std::find_if(_modulesCodeRanges.begin(), _modulesCodeRanges.end(),
         [&range](const ModuleCodeRange& r) {
            return r.startAddress == range.startAddress && r.endAddress == range.endAddress;
        });
        if (it != _modulesCodeRanges.end())
        {
            it->isRemoved = true;
        }
    }
}

std::vector<CodeRange> ManagedCodeCache::GetCodeRanges(FunctionID functionId)
{
    std::vector<CodeRange> result;
    constexpr size_t MAX_CODE_INFOS = 8;
    COR_PRF_CODE_INFO codeInfos[MAX_CODE_INFOS];
    ULONG32 nbCodeInfos;
    
    // For each code version of the function, there are at most 2 code ranges:
    // hot and cold.
    // For safety, we pass MAX_CODE_INFOS(8), even though we know it's 2.
    HRESULT hr = _profilerInfo->GetCodeInfo2(functionId, MAX_CODE_INFOS, &nbCodeInfos, codeInfos);
    
    if (FAILED(hr) || nbCodeInfos == 0)
    {
        return result;  // No code ranges
    }

    result.reserve(nbCodeInfos);
    for (ULONG32 i = 0; i < nbCodeInfos; i++)
    {
        if (codeInfos[i].size == 0)
        {
            continue;
        }
        result.emplace_back(
            codeInfos[i].startAddress,
            codeInfos[i].startAddress + codeInfos[i].size - 1,
            functionId);
    }
    
    return result;
}

void ManagedCodeCache::InsertCodeRangeIntoPage(PagesMap::iterator pageIt,
    const CodeRange& range)
{
    std::unique_lock<std::shared_timed_mutex> pageLock(pageIt->second.lock);
    auto& ranges = pageIt->second.ranges;
    ranges.insert(std::upper_bound(ranges.begin(), ranges.end(), range), range);
}

void ManagedCodeCache::AddFunctionRangesToCache(std::vector<CodeRange> newRanges)
{
    // Block profiler signals for the whole write so this thread cannot be
    // interrupted into a signal handler that would deadlock on the same mutex.
    ScopedProfilerSignalBlocker signalBlocker;

    for (const auto& range : newRanges)
    {
        // Method code range can span over 2 pages (in some weird cases, it could span over more than 2 pages).
        // We need to add the method code range in all the pages.
        uint64_t startPage = GetPageNumber(range.startAddress);
        uint64_t endPage = GetPageNumber(range.endAddress);
        for (uint64_t page = startPage; page <= endPage; ++page)
        {  
            // Check if page exists (with shared lock first - fast path)
            {
                std::shared_lock<std::shared_timed_mutex> mapLock(_pagesMutex);
                auto pageIt = _pagesMap.find(page);
                if (pageIt != _pagesMap.end())
                {
                    InsertCodeRangeIntoPage(pageIt, range);
                    continue;
                }
            }
            
            // Page doesn't exist, create it (with exclusive lock)
            std::unique_lock<std::shared_timed_mutex> mapLock(_pagesMutex);
            
            auto [pageIt, _] = _pagesMap.try_emplace(page);
            InsertCodeRangeIntoPage(pageIt, range);
        }
    }
}

void ManagedCodeCache::AddModuleRangesToCache(std::vector<ModuleCodeRange> moduleCodeRanges)
{
    // Block profiler signals for the whole write so this thread cannot be
    // interrupted into a signal handler that would deadlock on the same mutex.
    ScopedProfilerSignalBlocker signalBlocker;

    std::unique_lock<std::shared_timed_mutex> moduleLock(_modulesMutex);
    for (const auto& moduleCodeRange : moduleCodeRanges)
    {
        auto insertPos = std::upper_bound(
            _modulesCodeRanges.begin(),
            _modulesCodeRanges.end(),
            moduleCodeRange,
            [](const ModuleCodeRange& range, const ModuleCodeRange& other) {
                return range.startAddress < other.startAddress;
            });
        _modulesCodeRanges.insert(insertPos, moduleCodeRange);
    }
}

std::vector<ModuleCodeRange> ManagedCodeCache::GetModuleCodeRanges(ModuleID moduleId)
{
    std::vector<ModuleCodeRange> result;
    UINT_PTR baseLoadAddress = 0;
    DWORD moduleFlags;
    HRESULT hr = _profilerInfo->GetModuleInfo2(
        moduleId, reinterpret_cast<LPCBYTE*>(&baseLoadAddress), 0, NULL, NULL, NULL, &moduleFlags);

    if (FAILED(hr))
        return result;

    // We only register NGEN/R2R modules
    if ((moduleFlags & COR_PRF_MODULE_NGEN) != COR_PRF_MODULE_NGEN)
        return result;

    result.reserve(2);

    // PE parsing
    auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(baseLoadAddress);

    if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE)
    {
        return result;
    }

    UINT_PTR ntHeadersAddress = baseLoadAddress + dosHeader->e_lfanew;
    auto ntHeaders = reinterpret_cast<IMAGE_NT_HEADERS_GENERIC*>(ntHeadersAddress);

    if (ntHeaders->Signature != IMAGE_NT_SIGNATURE)
    {
        return result;
    }

    UINT_PTR sectionHeaderAddress = ntHeadersAddress 
        + sizeof(DWORD) + sizeof(IMAGE_FILE_HEADER) 
        + ntHeaders->FileHeader.SizeOfOptionalHeader;

    auto sectionHeaders = reinterpret_cast<PIMAGE_SECTION_HEADER>(sectionHeaderAddress);

    for (int i = 0; i < ntHeaders->FileHeader.NumberOfSections; i++)
    {
        if (sectionHeaders[i].Characteristics & IMAGE_SCN_MEM_EXECUTE)
        {
            UINT_PTR codeStart = baseLoadAddress + sectionHeaders[i].VirtualAddress;
            UINT_PTR codeEnd = codeStart + sectionHeaders[i].Misc.VirtualSize - 1;
            result.emplace_back(codeStart, codeEnd);
        }
    }
    return result;
}
