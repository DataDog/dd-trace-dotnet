// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedCodeCache.h"

#include "Configuration.h"

#include <algorithm>
#include <set>
#include <variant>

#include "Log.h"


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

ManagedCodeCache::ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo)
    : _profilerInfo(pProfilerInfo),
      _workerQueueEvent(false),
      _requestStop(false)
{
}

ManagedCodeCache::~ManagedCodeCache()
{
    if (_worker.joinable())
    {
        _requestStop = true;
        _workerQueueEvent.Set();
        _worker.join();
    }
}

bool ManagedCodeCache::Initialize()
{
    std::promise<void> startPromise;
    auto future = startPromise.get_future();
    _worker = std::thread(&ManagedCodeCache::WorkerThread, this, std::move(startPromise));
    if (future.wait_for(2s) == std::future_status::ready)
    {
        Log::Info("ManagedCodeCache initialized successfully");
        return true;
    }
    Log::Error("Failed to initialize ManagedCodeCache");
    return false;
}

bool ManagedCodeCache::IsCodeInR2RModule(std::uintptr_t ip) const noexcept
{
    std::shared_lock<std::shared_mutex> moduleLock(_modulesMutex);

    auto moduleCodeRange = FindRange(_modulesCodeRanges, ip);
    if (!moduleCodeRange.has_value())
    {
        return false;
    }

    if (moduleCodeRange->isRemoved)
    {
        LogOnce(Debug, "ManagedCodeCache::IsCodeInR2RModule: Module code range was removed for ip: 0x", std::hex, ip);
        return false;
    }

    return moduleCodeRange->contains(ip);
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
    
    if (IsCodeInR2RModule(ip))
    {
        auto functionId = GetFunctionFromIP_Original(ip);
        if (functionId.has_value()) {
            // We found a function id and we can add it synchronously to our cache.
            // It's safe to do this synchronously because this function is called
            // by a native thread belonging to profiler.
            // This thread won't be interrupted by the profiler.
            AddFunctionImpl(functionId.value(), false);
            return std::optional<FunctionID>(functionId.value());
        }
        // If we arrive here, it means that the call to GetFunctionFromIP_Original possibly crashed.
        // Possible reason: race against the CLR unloading the module containing the function.
        // On Windows, we catch the exception and return nullopt.
        // On Linux, we cannot do anything.
        // Maybe a better value ?
        return std::optional<FunctionID>(InvalidFunctionId);
    }

    return std::nullopt;
}

std::optional<FunctionID> ManagedCodeCache::GetFunctionFromIP_Original(std::uintptr_t ip) noexcept
{
    FunctionID functionId;

    // On Windows, the call to GetFunctionFromIP can crash:
    // We may end up in a situation where the module containing that symbol was just unloaded.
    // For linux, we use the custom GetFunctionFromIP based on the code cache.
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
    }
#endif

    return std::nullopt;
}

std::optional<FunctionID> ManagedCodeCache::GetFunctionIdImpl(std::uintptr_t ip) const noexcept
{
    uint64_t page = GetPageNumber(static_cast<UINT_PTR>(ip));
    
    // Level 1: Find the page (shared lock on map structure)
    std::shared_lock<std::shared_mutex> mapLock(_pagesMutex);
    auto pageIt = _pagesMap.find(page);
    if (pageIt == _pagesMap.end())
    {
        return std::nullopt;  // No code on this page
    }
    
    // Level 2: Binary search within the page's ranges (shared lock on page)
    std::shared_lock<std::shared_mutex> pageLock(pageIt->second.lock);
    auto range = FindRange(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
    if (range.has_value())
    {
        return range->functionId;
    }
    
    return std::nullopt;
}

// can be called in a signal handler
bool ManagedCodeCache::IsManaged(std::uintptr_t ip) const noexcept
{
    uint64_t page = GetPageNumber(static_cast<UINT_PTR>(ip));
    
    {
        // Level 1: Find the page (shared lock on map structure)
        std::shared_lock<std::shared_mutex> mapLock(_pagesMutex);
        auto pageIt = _pagesMap.find(page);
        if (pageIt == _pagesMap.end())
        {
            return false;  // No code on this page
        }
        
        // Level 2: Binary search within the page's ranges (shared lock on page)
   
        std::shared_lock<std::shared_mutex> pageLock(pageIt->second.lock);
        auto range = FindRange(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
        if (range.has_value())
        {
            return true;
        }
    }

    // Check if the IP is within a module code range
    return IsCodeInR2RModule(ip);
}

void ManagedCodeCache::AddFunction(FunctionID functionId)
{
    AddFunctionImpl(functionId, true);
}

// Maybe rename this into OnJitCompilation
void ManagedCodeCache::AddFunctionImpl(FunctionID functionId, bool isAsync)
{
    auto ranges = GetCodeRanges(functionId);

    if (ranges.empty())
    {
        return;
    }

    if (isAsync)
    {
        // IMPORTANT: Defer to background thread to avoid deadlock
        //
        // Why we can't do this synchronously:
        // 1. JITCompilationFinished is called on a managed thread (Thread A)
        // 2. If we called AppendRangesToCache directly here, Thread A would acquire 
        //    writer lock on PageEntry
        // 3. CPU profiler signal could interrupt Thread A (async)
        // 4. Signal handler calls IsManaged, tries to acquire reader lock on same PageEntry
        // 5. DEADLOCK: Reader lock waits for writer lock held by same thread
        //
        // Solution: Enqueue work to background thread. Thread A never acquires PageEntry 
        // locks, so signal handler can safely acquire locks without deadlock.
        AddFunctionCodeRangesAsync(std::move(ranges));
    }
    else
    {
        AddFunctionRangesToCache(std::move(ranges));
    }
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

    // IMPORTANT: Defer to background thread to avoid deadlock
    //
    // Scenario: ModuleLoadFinished is called on a managed thread (Thread A)
    // 1. Thread A calls AddModuleRanges, acquires writer lock if done synchronously
    // 2. CPU profiler signal interrupts Thread A (async)
    // 3. Signal handler calls IsManaged, tries to acquire reader lock
    // 4. DEADLOCK: Reader lock waits for writer lock held by same thread
    //
    // Solution: Enqueue work to background thread. Thread A holds no locks
    // when it returns from this callback, so signal handler can safely
    // acquire locks.
    //
    // Additional benefit: Reduces callback latency and avoids holding
    // CLR-internal locks longer than necessary.
    AddModuleCodeRangesAsync(std::move(moduleCodeRanges));
}

void ManagedCodeCache::RemoveModule(ModuleID moduleId)
{
    auto moduleCodeRanges = GetModuleCodeRanges(moduleId);
    if (moduleCodeRanges.empty())
    {
        return;
    }

    std::unique_lock<std::shared_mutex> moduleLock(_modulesMutex);
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
    std::unique_lock<std::shared_mutex> pageLock(pageIt->second.lock);
    auto& ranges = pageIt->second.ranges;
    ranges.insert(std::upper_bound(ranges.begin(), ranges.end(), range), range);
}

// This function must be called by the worker thread only
void ManagedCodeCache::AddFunctionRangesToCache(std::vector<CodeRange> newRanges)
{
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
                std::shared_lock<std::shared_mutex> mapLock(_pagesMutex);
                auto pageIt = _pagesMap.find(page);
                if (pageIt != _pagesMap.end())
                {
                    InsertCodeRangeIntoPage(pageIt, range);
                    continue;
                }
            }
            
            // Page doesn't exist, create it (with exclusive lock)
            std::unique_lock<std::shared_mutex> mapLock(_pagesMutex);
            
            auto [pageIt, _] = _pagesMap.try_emplace(page);
            InsertCodeRangeIntoPage(pageIt, range);
        }
    }
}

void ManagedCodeCache::AddModuleRangesToCache(std::vector<ModuleCodeRange> moduleCodeRanges)
{
    std::unique_lock<std::shared_mutex> moduleLock(_modulesMutex);
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

void ManagedCodeCache::EnsurePageExists(uint64_t page)
{
    // Check if page exists (with shared lock first - fast path)
    {
        std::shared_lock<std::shared_mutex> mapLock(_pagesMutex);
        if (_pagesMap.find(page) != _pagesMap.end())
        {
            return;  // Already exists
        }
    }
    
    // Page doesn't exist, create it (with exclusive lock)
    std::unique_lock<std::shared_mutex> mapLock(_pagesMutex);
    
    // Double-check after acquiring exclusive lock (another thread might have created it)
    if (_pagesMap.find(page) == _pagesMap.end())
    {
        _pagesMap[page] = PageEntry();
    }
}

// Template struct for appending ranges work items
template<typename T>
struct AppendRangesWork
{
    std::vector<T> ranges;
    
    explicit AppendRangesWork(std::vector<T> r) 
        : ranges(std::move(r)) {}
};

// Type aliases for specific work item types
using AppendCodeRangesWork = AppendRangesWork<CodeRange>;
using AppendModuleRangesWork = AppendRangesWork<ModuleCodeRange>;

void ManagedCodeCache::WorkerThread(std::promise<void> startPromise)
{
    startPromise.set_value();

    while (!_requestStop)
    {
        _workerQueueEvent.Wait();

        if (_requestStop)
        {
            break;
        }

        std::forward_list<std::function<void()>> workItems;
        {
            std::unique_lock<std::mutex> lock(_queueMutex);
            std::swap(_workerQueue, workItems);
        }

        for (auto& workItem : workItems)
        {
            workItem();
        }
    }
}

// Private helper template
template<typename WorkType>
void ManagedCodeCache::EnqueueWork(WorkType work)
{
    auto workFunction = [this, work = std::move(work)]() mutable{
        using T = std::decay_t<WorkType>;
        
        if constexpr (std::is_same_v<T, AppendCodeRangesWork>) {
            AddFunctionRangesToCache(std::move(work.ranges));
        }
        else if constexpr (std::is_same_v<T, AppendModuleRangesWork>) {
            AddModuleRangesToCache(std::move(work.ranges));
        }
    };

    {
        std::unique_lock<std::mutex> lock(_queueMutex);
        _workerQueue.push_front(std::move(workFunction));
    }
    
    _workerQueueEvent.Set();
}

void ManagedCodeCache::AddFunctionCodeRangesAsync(std::vector<CodeRange> ranges)
{
    EnqueueWork(AppendCodeRangesWork(std::move(ranges)));
}

void ManagedCodeCache::AddModuleCodeRangesAsync(std::vector<ModuleCodeRange> moduleCodeRanges)
{
    EnqueueWork(AppendModuleRangesWork(std::move(moduleCodeRanges)));
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