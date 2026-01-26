// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedCodeCache.h"

#include "Configuration.h"

#include <algorithm>
#include <set>
#include <variant>

#include "Log.h"

// PE32 and PE64 have different optional headers, which complexify the logic to fetch them
// This struct contains the common fields between the two types of headers
struct IMAGE_NT_HEADERS_GENERIC
{
    DWORD Signature;
    IMAGE_FILE_HEADER FileHeader;
    WORD    Magic;
};

ManagedCodeCache::ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo, IConfiguration* pConfiguration)
    : _profilerInfo(pProfilerInfo),
      _workerQueueEvent(false),
      _useCustomGetFunctionFromIP{pConfiguration->UseCustomGetFunctionFromIP()}
{
    Log::Info("ManagedCodeCache initialized with useCustomGetFunctionFromIP: ", std::boolalpha, _useCustomGetFunctionFromIP);
}

ManagedCodeCache::~ManagedCodeCache()
{
    // clean up the work queue
}

bool ManagedCodeCache::StartImpl()
{
    std::promise<void> startPromise;
    auto future = startPromise.get_future();
    _worker = std::thread(&ManagedCodeCache::WorkerThread, this, std::move(startPromise));
    return future.wait_for(2s) == std::future_status::ready;
}

bool ManagedCodeCache::StopImpl()
{
    _requestStop = true;
    _workerQueueEvent.Set();
    _worker.join();
    // clean up the work queue
    return true;
}

const char* ManagedCodeCache::GetName()
{
    return "ManagedCodeCache";
}

bool ManagedCodeCache::IsCodeInR2RModule(std::uintptr_t ip) const noexcept
{
    std::shared_lock<std::shared_mutex> moduleLock(_modulesMutex);
    
    // Use lower_bound to find the first range where startAddress > ip
    // The range we're looking for (if it exists) must be the PREVIOUS range
    auto moduleIt = std::lower_bound(
        _modulesCodeRanges.begin(),
        _modulesCodeRanges.end(),
        ip,
        [](const ModuleCodeRange& range, std::uintptr_t ip)
        {
            // Return true if range comes before ip
            return range.startAddress <= ip;
        });
    
    // lower_bound returns first range where startAddress > ip
    // By definition, that range cannot contain ip
    // So we only need to check the PREVIOUS range
    if (moduleIt == _modulesCodeRanges.begin())
    {
        return false;
    }
    
    --moduleIt;
    
    if (moduleIt->isRemoved)
    {
        LogOnce(Debug, "ManagedCodeCache::IsCodeInR2RModule: Module code range is removed for ip: 0x", std::hex, ip);
        return false;
    }

    return moduleIt->contains(ip);
}

// must not be called in a signal handler (GetFunctionFromIP is not signal-safe)
// nor by a managed thread (is that really a valid constraint?)
std::optional<FunctionID> ManagedCodeCache::GetFunctionId(std::uintptr_t ip) noexcept
{
    if (!_useCustomGetFunctionFromIP)
    {
        return GetFunctionFromIP_Original(ip);
    }

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
            // in that case no need to defer this action to another thread
            // if we let another thread do this, we can remove the constraint on managed threads
            AddFunctionImpl(*functionId, false);
            return std::optional<FunctionID>(*functionId);
        }
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
    const CodeRange* range = FindRangeInVector(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
    if (range != nullptr)
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
        const CodeRange* range = FindRangeInVector(pageIt->second.ranges, static_cast<UINT_PTR>(ip));
        if (range != nullptr)
        {
            return true;
        }
    }

    // Check if the IP is within a module code range
    return IsCodeInR2RModule(ip);
}

// Binary search helper (signal-safe, no allocation)
const CodeRange* ManagedCodeCache::FindRangeInVector(
    const std::vector<CodeRange>& ranges,
    UINT_PTR ip) noexcept
{
    if (ranges.empty()) return nullptr;
    
    // Use lower_bound to find the first range where startAddress > ip
    // The range we're looking for (if it exists) must be the PREVIOUS range
    auto it = std::lower_bound(
        ranges.begin(),
        ranges.end(),
        ip,
        [](const CodeRange& range, UINT_PTR ip)
        {
            // Return true if range comes before ip
            // This means range.startAddress <= ip
            return range.startAddress <= ip;
        });
    
    // lower_bound returns first range where startAddress > ip
    // By definition, that range cannot contain ip
    // So we only need to check the PREVIOUS range
    if (it != ranges.begin())
    {
        --it;
        if (it->contains(ip))
        {
            return &(*it);
        }
    }
    
    return nullptr;
}

void ManagedCodeCache::AddFunction(FunctionID functionId)
{
    AddFunctionImpl(functionId, true);
}

// Maybe rename this into OnJitCompilation
void ManagedCodeCache::AddFunctionImpl(FunctionID functionId, bool isAsync)
{
    auto ranges = QueryCodeRanges(functionId);

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

std::vector<CodeRange> ManagedCodeCache::QueryCodeRanges(FunctionID functionId)
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
            codeInfos[i].startAddress + codeInfos[i].size,
            functionId);
    }
    
    return result;
}

// This function must be called by the worker thread only
void ManagedCodeCache::AddFunctionRangesToCache(std::vector<CodeRange> newRanges)
{    
    // Step 1: Compute affected pages
    std::set<uint64_t> affectedPages;
    for (const auto& range : newRanges)
    {
        uint64_t startPage = GetPageNumber(range.startAddress);
        uint64_t endPage = GetPageNumber(range.endAddress - 1);
        for (uint64_t p = startPage; p <= endPage; ++p) {
            affectedPages.insert(p);
        }
    }
    
    // Step 2: Ensure all affected pages exist
    for (uint64_t page : affectedPages)
    {
        // I do not understand why the page would not exist :thinking:
        EnsurePageExists(page);
    }
    
    // Step 3: Add ranges to each affected page (keep sorted)
    std::shared_lock<std::shared_mutex> mapLock(_pagesMutex);
    for (uint64_t page : affectedPages)
    {
        auto pageIt = _pagesMap.find(page);
        if (pageIt == _pagesMap.end())
        {
            continue;  // Shouldn't happen, but be safe
        }
        
        // Lock only this page for update
        std::unique_lock<std::shared_mutex> pageLock(pageIt->second.lock);
        
        // Add new ranges to this page (keep sorted)
        for (const auto& newRange : newRanges)
        {
            uint64_t startPage = GetPageNumber(newRange.startAddress);
            uint64_t endPage = GetPageNumber(newRange.endAddress - 1);
            
            if (page >= startPage && page <= endPage)
            {
                auto& ranges = pageIt->second.ranges;
                
                // Insert in sorted position
                auto insertPos = std::upper_bound(
                    ranges.begin(),
                    ranges.end(),
                    newRange);
                ranges.insert(insertPos, newRange);
            }
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

// Work item is a variant of different types
using WorkItem = std::variant<AppendCodeRangesWork, AppendModuleRangesWork>;

struct ManagedCodeCache::QueueNode
{
    WorkItem work;
    QueueNode* next;
    explicit QueueNode(WorkItem w) 
        : work(std::move(w)), next(nullptr) {}
};

std::unique_ptr<ManagedCodeCache::QueueNode> ManagedCodeCache::DequeueWorkItem()
{
    auto* currentHead = _workerQueueHead.load(std::memory_order_acquire);
    while (currentHead != nullptr)
    {
        auto* next = currentHead->next;
        if (_workerQueueHead.compare_exchange_weak(
            currentHead,
            next,
            std::memory_order_release,
            std::memory_order_acquire))
        {
            return std::unique_ptr<ManagedCodeCache::QueueNode>(currentHead);
        }
    }
    return nullptr;
}

void ManagedCodeCache::WorkerThread(std::promise<void> startPromise)
{
    auto visitWork = [this](auto&& work)
    {
        using T = std::decay_t<decltype(work)>;
        
        if constexpr (std::is_same_v<T, AppendCodeRangesWork>) {
            // Handle code ranges
            AddFunctionRangesToCache(std::move(work.ranges));
        }
        else if constexpr (std::is_same_v<T, AppendModuleRangesWork>) {
            // Handle module ranges
            AddModuleRangesToCache(std::move(work.ranges));
        }
    };

    startPromise.set_value();

    while (!_requestStop)
    {

        _workerQueueEvent.Wait();

        if (_requestStop)
        {
            break;
        }

        while (true)
        {
            auto node = DequeueWorkItem();

            if (node == nullptr)
            {
                break;
            }
            // Use std::visit to handle different work item types
            std::visit(visitWork, node->work);
        }
    }
}

// Private helper template
template<typename WorkType>
void ManagedCodeCache::EnqueueWork(WorkType work)
{
    auto node = std::make_unique<QueueNode>(std::move(work));
    auto* rawNode = node.get();
    auto* currentHead = _workerQueueHead.load(std::memory_order_acquire);
    
    do
    {
        rawNode->next = currentHead;
    } while (!_workerQueueHead.compare_exchange_weak(
        currentHead,
        rawNode,
        std::memory_order_release,
        std::memory_order_acquire));
    
    // Succeeded, we transferred ownership to the atomic pointer
    node.release();
    
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
            UINT_PTR codeEnd = codeStart + sectionHeaders[i].Misc.VirtualSize;
            result.emplace_back(codeStart, codeEnd);
        }
    }
    return result;
}