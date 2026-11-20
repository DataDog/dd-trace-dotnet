// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ManagedCodeCache.h"

#include "Log.h"

#include <algorithm>
#include <sstream>

// PE32 and PE64 have different optional headers, which complexify the logic to fetch them
// This struct contains the common fields between the two types of headers
struct IMAGE_NT_HEADERS_GENERIC
{
    DWORD Signature;
    IMAGE_FILE_HEADER FileHeader;
    WORD    Magic;
};

ManagedCodeCache::ManagedCodeCache(ICorProfilerInfo4* pProfilerInfo) :
    _codeRanges(_reclaimer),
    _modules(_reclaimer),
    _profilerInfo(pProfilerInfo)
{
}

ManagedCodeCache::~ManagedCodeCache() = default;

bool ManagedCodeCache::Initialize()
{
    Log::Info("ManagedCodeCache initialized successfully");
    return true;
}

// can be called in a signal handler
bool ManagedCodeCache::IsCodeInR2RModule(std::uintptr_t ip) const noexcept
{
    EpochReclaimer::ReadGuard guard(_reclaimer);
    return _modules.Contains(ip, guard);
}

// must not be called in a signal handler (GetFunctionFromIP is not signal-safe)
// nor by a managed thread (is that really a valid constraint?)
std::optional<ManagedCodeCache::FunctionInfo> ManagedCodeCache::GetFunctionInfo(std::uintptr_t ip) noexcept
{
    // Called by a profiler thread (never from a signal handler): good place to free
    // the memory retired by writers that could not be freed at that time because
    // readers were active (otherwise it waits for the next write).
    _reclaimer.TryReclaimIfPending();

    auto info = GetFunctionInfoImpl(ip);
    if (info.has_value())
    {
        return info;
    }

    // Level 2: Check if the IP is within a module code range

    if (!IsCodeInR2RModule(ip))
    {
        // if it has value `false`, just return InvalidFunctionId
        return FunctionInfo{InvalidFunctionId, false};
    }

    auto functionId = GetFunctionFromIP_Original(ip);
    if (functionId.has_value() && functionId.value() != InvalidFunctionId) {
        // We found a function id and we can add it synchronously to our cache.
        // Precompiled (R2R/NGEN) code is never dynamic: a dynamic method only exists at runtime.
        AddFunctionImpl(functionId.value(), /*isDynamic*/ false);
        return FunctionInfo{functionId.value(), false};
    }
    // If we arrive here, it means that the call to GetFunctionFromIP_Original possibly crashed.
    // Possible reason: race against the CLR unloading the module containing the function.
    // On Windows, we catch the exception and return nullopt.
    // On Linux, we cannot do anything, we'll never get there.

    if (!functionId.has_value())
    {
        return std::nullopt;
    }
    return FunctionInfo{functionId.value(), false};
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

std::optional<ManagedCodeCache::FunctionInfo> ManagedCodeCache::GetFunctionInfoImpl(std::uintptr_t ip) const noexcept
{
    EpochReclaimer::ReadGuard guard(_reclaimer);
    auto range = _codeRanges.Find(ip, guard);
    if (range.has_value())
    {
        return FunctionInfo{static_cast<FunctionID>(range->functionId), range->isDynamic};
    }

    return std::nullopt;
}

// can be called in a signal handler: lock-free, no allocation
std::optional<bool> ManagedCodeCache::IsManaged(std::uintptr_t ip) const noexcept
{
    ReadScope scope(_reclaimer);
    return IsManaged(ip, scope);
}

// can be called in a signal handler: lock-free, no allocation
std::optional<bool> ManagedCodeCache::IsManaged(std::uintptr_t ip, const ReadScope& scope) const noexcept
{
    if (_codeRanges.Contains(ip, scope._guard))
    {
        return true;
    }

    // IP not in any JIT-compiled range: check R2R modules
    return _modules.Contains(ip, scope._guard);
}

void ManagedCodeCache::AddFunction(FunctionID functionId, bool isDynamic)
{
    AddFunctionImpl(functionId, isDynamic);
}

// Maybe rename this into OnJitCompilation
void ManagedCodeCache::AddFunctionImpl(FunctionID functionId, bool isDynamic)
{
    auto ranges = GetCodeRanges(functionId, isDynamic);

    if (ranges.empty())
    {
        return;
    }

    // The write is performed synchronously. Readers (signal handlers) never take
    // the writer lock, so there is no need to block the profiler signals here.
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

    AddModuleRangesToCache(std::move(moduleCodeRanges));
}

void ManagedCodeCache::RemoveModule(ModuleID moduleId)
{
    auto moduleCodeRanges = GetModuleCodeRanges(moduleId);
    if (moduleCodeRanges.empty())
    {
        return;
    }

    RemoveModuleRangesFromCache(std::move(moduleCodeRanges));
}

std::vector<CodeRange> ManagedCodeCache::GetCodeRanges(FunctionID functionId, bool isDynamic)
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
            functionId,
            isDynamic);
    }

    return result;
}

void ManagedCodeCache::AddFunctionRangesToCache(std::vector<CodeRange> newRanges)
{
    for (const auto& range : newRanges)
    {
        if (!_codeRanges.Insert(range))
        {
            LogOnce(Warn, "ManagedCodeCache::AddFunctionRangesToCache: code range outside of the indexable address space ignored: [0x",
                    std::hex, range.startAddress, " - 0x", range.endAddress, "]");
        }
    }
}

void ManagedCodeCache::AddModuleRangesToCache(std::vector<ModuleCodeRange> moduleCodeRanges)
{
    // Keep the module ranges consistent with what the code range trie can index
    auto it = std::remove_if(moduleCodeRanges.begin(), moduleCodeRanges.end(), [](const ModuleCodeRange& range) {
        return range.endAddress < range.startAddress || !CodeRangeTrie::IsIndexable(range.endAddress);
    });
    if (it != moduleCodeRanges.end())
    {
        LogOnce(Warn, "ManagedCodeCache::AddModuleRangesToCache: module code range outside of the indexable address space ignored: [0x",
                std::hex, it->startAddress, " - 0x", it->endAddress, "]");
        moduleCodeRanges.erase(it, moduleCodeRanges.end());
    }

    _modules.Add(moduleCodeRanges.data(), moduleCodeRanges.size());

    // Fast path for IsManaged: the pages fully covered by the module are marked in the
    // code range trie (the ModuleRangeSet stays the reference for the partial pages)
    for (auto const& range : moduleCodeRanges)
    {
        _codeRanges.MarkModulePages(range, true);
    }
}

void ManagedCodeCache::RemoveModuleRangesFromCache(std::vector<ModuleCodeRange> moduleCodeRanges)
{
    for (auto const& range : moduleCodeRanges)
    {
        _codeRanges.MarkModulePages(range, false);
    }
    _modules.Remove(moduleCodeRanges.data(), moduleCodeRanges.size());
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
