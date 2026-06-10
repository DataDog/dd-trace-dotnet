// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "IConfiguration.h"
#include "Log.h"
#include "MeanMaxMetric.h"
#include "MetricsRegistry.h"
#include "OpSysTools.h"
#include "ProxyMetric.h"

#include <algorithm>
#include <chrono>
#include <elf.h>
#include <fcntl.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <time.h>
#include <unistd.h>

using namespace std::chrono_literals;

namespace {

struct ScopedMmap
{
    void* data;
    size_t size;

    ScopedMmap(int fd, size_t len) : data(MAP_FAILED), size(len)
    {
        data = mmap(nullptr, size, PROT_READ, MAP_PRIVATE, fd, 0);
        posix_fadvise(fd, 0, len, POSIX_FADV_DONTNEED);
        close(fd);
    }

    ~ScopedMmap() { if (data != MAP_FAILED) munmap(data, size); }

    ScopedMmap(const ScopedMmap&) = delete;
    ScopedMmap& operator=(const ScopedMmap&) = delete;

    explicit operator bool() const { return data != MAP_FAILED; }
};

} // anonymous namespace

// --------------------------------------------------------------------------
// TrackingMemoryResource: wraps an upstream allocator to track memory usage.
// --------------------------------------------------------------------------

class TrackingMemoryResource : public shared::pmr::memory_resource
{
public:
    explicit TrackingMemoryResource(shared::pmr::memory_resource* upstream) :
        _upstream{upstream}
    {
    }

    std::size_t GetCurrentUsage() const { return _currentUsage.load(std::memory_order_relaxed); }

protected:
    void* do_allocate(std::size_t bytes, std::size_t alignment) override
    {
        _currentUsage.fetch_add(bytes, std::memory_order_relaxed);
        return _upstream->allocate(bytes, alignment);
    }

    void do_deallocate(void* p, std::size_t bytes, std::size_t alignment) override
    {
        _currentUsage.fetch_sub(bytes, std::memory_order_relaxed);
        _upstream->deallocate(p, bytes, alignment);
    }

    bool do_is_equal(const memory_resource& other) const noexcept override
    {
        return this == &other;
    }

private:
    shared::pmr::memory_resource* _upstream;
    std::atomic<std::size_t> _currentUsage{0};
};

// --------------------------------------------------------------------------
// FootprintTracker: encapsulates all memory/CPU tracking and metrics.
// Only instantiated when IsMemoryFootprintEnabled() is true.
// --------------------------------------------------------------------------

struct FootprintTracker
{
    TrackingMemoryResource trackingResource;

    std::uint32_t reloadCount{0};
    std::uint64_t totalCpuNs{0};

    std::atomic<uint32_t> libCount{0};
#ifdef ARM64
    std::atomic<uint32_t> moduleRegionCount{0};
    std::atomic<uint32_t> symbolCount{0};
#endif

    std::shared_ptr<ProxyMetric> libCountMetric;
    std::shared_ptr<ProxyMetric> memoryFootprintMetric;
#ifdef ARM64
    std::shared_ptr<ProxyMetric> moduleCountMetric;
    std::shared_ptr<ProxyMetric> symbolCountMetric;
#endif
    std::shared_ptr<MeanMaxMetric> updateCpuMetric;
    std::shared_ptr<MeanMaxMetric> reloadDurationMetric;
    std::shared_ptr<MeanMaxMetric> lockHoldDurationMetric;

    explicit FootprintTracker(shared::pmr::memory_resource* upstream) :
        trackingResource{upstream}
    {
    }

    void RegisterMetrics(MetricsRegistry& registry);
    void LogStats(std::size_t libCount);
    void RecordReload(std::chrono::steady_clock::duration reloadDuration,
                      std::chrono::steady_clock::duration lockDuration,
                      struct timespec cpuStart, struct timespec cpuEnd);
};

// --------------------------------------------------------------------------
// LibrariesInfoCache
// --------------------------------------------------------------------------

std::atomic<LibrariesInfoCache*> LibrariesInfoCache::s_instance{nullptr};

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

LibrariesInfoCache::LibrariesInfoCache(IConfiguration* configuration, shared::pmr::memory_resource* resource, MetricsRegistry& metricsRegistry) :
    _tracker{configuration->IsMemoryFootprintEnabled() ? std::make_unique<FootprintTracker>(resource) : nullptr},
    _wrappersAllocator{_tracker ? &_tracker->trackingResource : resource},
    _librariesInfo{_wrappersAllocator},
    _newCache{_wrappersAllocator},
#ifdef ARM64
    _moduleRegions{_wrappersAllocator},
    _symbols{_wrappersAllocator},
    _newRegions{_wrappersAllocator},
    _newSymbols{_wrappersAllocator},
#endif
    _stopRequested{false},
    _event(true)
{
    if (_tracker)
    {
        _tracker->RegisterMetrics(metricsRegistry);
    }
}

LibrariesInfoCache::~LibrariesInfoCache() = default;

const char* LibrariesInfoCache::GetName()
{
    return "Libraries Info Cache";
}

bool LibrariesInfoCache::StartImpl()
{
    _librariesInfo.reserve(100);

    // CRITICAL ORDERING:
    // 1. Start the worker thread to populate the cache
    // 2. Wait for the first cache update to complete
    // 3. Set s_instance ONLY after cache is populated
    // 4. THEN register the custom function with libunwind
    //
    // This ensures s_instance is NEVER set with an empty cache, and when the
    // custom function is registered and could be called from signal handlers,
    // both s_instance is set AND the cache is fully populated.

    auto startEvent = std::make_shared<AutoResetEvent>(false);
    _worker = std::thread(&LibrariesInfoCache::Work, this, startEvent);

    // Wait for the thread to be fully started and the cache populated
    // before setting s_instance and registering with libunwind.
    // Cache population can be slow under sanitizers (ASAN/UBSAN add overhead to
    // every allocation and memory access) — use a longer timeout in that case.
#if defined(DD_SANITIZERS)
    constexpr auto startTimeout = 10s;
#else
    constexpr auto startTimeout = 2s;
#endif
    if (!startEvent->Wait(startTimeout))
    {
        Log::Error("Failed to populate LibrariesInfoCache within timeout. "
                   "Not registering custom iterate_phdr_function with libunwind.");
        _stopRequested = true;
        _event.Set();
        _worker.join();
        return false;
    }

    // Now that the cache is populated, set the instance pointer
    s_instance.store(this, std::memory_order_release);

    // Register the custom iterate_phdr with libunwind.
    // Note: unw_set_iterate_phdr_function will call tdep_init() if needed,
    // which has once-initialization semantics, so this is thread-safe.
    unw_set_iterate_phdr_function(unw_local_addr_space, LibrariesInfoCache::DlIteratePhdr);

#ifdef ARM64
    // On ARM64, unw_step's fallback path (get_frame_state) calls get_proc_name
    // on every step when DWARF info is missing. The default get_static_proc_name
    // does expensive /proc/maps parsing + ELF mmap + symbol scan each time.
    // Replace it with our cached binary-search implementation.
    unw_accessors_t* acc = unw_get_accessors(unw_local_addr_space);
    _originalGetProcName = acc->get_proc_name;
    acc->get_proc_name = LibrariesInfoCache::GetProcName;
#endif

    // CRITICAL: Force a memory barrier to ensure the writes to iterate_phdr_function
    // (and get_proc_name on ARM64) are visible to all CPU cores. libunwind's setters
    // do plain pointer writes with no memory barriers, which can cause the signal
    // handler on a different CPU core to see old values.
    std::atomic_thread_fence(std::memory_order_seq_cst);

    Log::Info("LibrariesInfoCache: Registered custom iterate_phdr_function with libunwind.");

    return true;
}

bool LibrariesInfoCache::StopImpl()
{
    // Clear s_instance first so signal handlers stop using our accessors
    s_instance.store(nullptr, std::memory_order_release);
    std::atomic_thread_fence(std::memory_order_seq_cst);

    unw_set_iterate_phdr_function(unw_local_addr_space, dl_iterate_phdr);

#ifdef ARM64
    if (_originalGetProcName != nullptr)
    {
        unw_accessors_t* acc = unw_get_accessors(unw_local_addr_space);
        acc->get_proc_name = _originalGetProcName;
        _originalGetProcName = nullptr;
    }
#endif

    _stopRequested = true;
    NotifyCacheUpdateImpl();
    Log::Debug("Notification to stop the worker has been sent.");
    _worker.join();

    if (_tracker)
    {
        _tracker->LogStats(_librariesInfo.size());
    }

    _librariesInfo.clear();
#ifdef ARM64
    _moduleRegions.clear();
    _symbols.clear();
#endif

    return true;
}

void LibrariesInfoCache::Work(std::shared_ptr<AutoResetEvent> startEvent)
{
    OpSysTools::SetNativeThreadName(WStr("DD_LibsCache"));

    auto timeout = InfiniteTimeout;
    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = LibrariesInfoCache::NotifyCacheUpdate;
    }
    else
    {
        // if this function is missing we still want the cache to update on a regular basis
        constexpr auto defaultTimeout = 1s;
        Log::Info("Wrapper library is missing. The cache will reload itself every ", defaultTimeout);
        timeout = defaultTimeout;
    }

    while (!_stopRequested)
    {
        // in the default case, notification mechanism in place, we will block until notification
        // Otherwise, we reload the cache no matter on a regular basis (defaultTimeout)
        _event.Wait(timeout);

        if (_stopRequested)
        {
            break;
        }

        UpdateCache();

        if (startEvent != nullptr)
        {
            startEvent->Set();
            startEvent.reset();
        }
    }

    Log::Debug("Stopping worker: stop request received.");

    if (&dd_notify_libraries_cache_update != nullptr) [[likely]]
    {
        dd_notify_libraries_cache_update = nullptr;
    }
}

void LibrariesInfoCache::UpdateCache()
{
    // We *MUST* not allocate while holding the _cacheLock
    // We may end up in a deadlock situation
    // Example:
    // T1 tries to allocate and is interrupted by the sampler
    // Cache Thread, updates the cache and allocates while holding the cache lock.
    // But the Cache Thread is blocked (T1 owns the malloc lock)
    // T1 is blocked when libwunding calls DlIteratePhdr.

    struct timespec cpuStart = {};
    std::chrono::steady_clock::time_point reloadStart;
    if (_tracker)
    {
        _tracker->reloadCount++;
        clock_gettime(CLOCK_THREAD_CPUTIME_ID, &cpuStart);
        reloadStart = std::chrono::steady_clock::now();
    }

    _newCache.reserve(_librariesInfo.capacity());

    struct Data
    {
    public:
        std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>>* Cache;
        shared::pmr::memory_resource* Allocator;
    };

    auto data = Data{.Cache = &_newCache, .Allocator = _wrappersAllocator};

    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* impl = static_cast<Data*>(data);
            impl->Cache->emplace_back(info, size, impl->Allocator);
            return 0;
        },
        &data);

#ifdef ARM64
    _newRegions.clear();
    _newSymbols.clear();
    BuildSymbolCache(_newCache, _newRegions, _newSymbols);
#endif

    std::chrono::steady_clock::time_point lockStart;
    if (_tracker)
    {
        lockStart = std::chrono::steady_clock::now();
    }
    {
        std::unique_lock l{_cacheLock};
        _librariesInfo.swap(_newCache);
#ifdef ARM64
        _moduleRegions.swap(_newRegions);
        _symbols.swap(_newSymbols);
#endif
        if (_tracker)
        {
            _tracker->libCount.store(static_cast<uint32_t>(_librariesInfo.size()), std::memory_order_relaxed);
#ifdef ARM64
            _tracker->moduleRegionCount.store(static_cast<uint32_t>(_moduleRegions.size()), std::memory_order_relaxed);
            _tracker->symbolCount.store(static_cast<uint32_t>(_symbols.size()), std::memory_order_relaxed);
#endif
        }
    }
    std::chrono::steady_clock::time_point lockEnd;
    if (_tracker)
    {
        lockEnd = std::chrono::steady_clock::now();
    }

    _newCache.clear();
#ifdef ARM64
    _newRegions.clear();
    _newSymbols.clear();
#endif

    if (_tracker)
    {
        auto reloadEnd = std::chrono::steady_clock::now();
        auto reloadDuration = reloadEnd - reloadStart;
        auto lockDuration = lockEnd - lockStart;

        struct timespec cpuEnd = {};
        clock_gettime(CLOCK_THREAD_CPUTIME_ID, &cpuEnd);

        _tracker->RecordReload(reloadDuration, lockDuration, cpuStart, cpuEnd);
    }
}

#ifdef ARM64
void LibrariesInfoCache::BuildSymbolCache(
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>>& phdrCache,
    std::vector<ModuleRegion, shared::pmr::polymorphic_allocator<ModuleRegion>>& outRegions,
    std::vector<FuncEntry, shared::pmr::polymorphic_allocator<FuncEntry>>& outSymbols)
{
    struct AbsFuncEntry
    {
        unw_word_t start_ip;
        unw_word_t end_ip;
    };
    std::vector<AbsFuncEntry> moduleSymbols;

    for (auto& wrapper : phdrCache)
    {
        auto [info, size] = wrapper.Get();

        const char* modulePath = info->dlpi_name;
        if (modulePath == nullptr || modulePath[0] == '\0')
        {
            modulePath = "/proc/self/exe";
        }

        unw_word_t segLow = 0;
        unw_word_t segHigh = 0;
        bool foundExecSeg = false;
        for (int i = 0; i < info->dlpi_phnum; ++i)
        {
            const auto& phdr = info->dlpi_phdr[i];
            if (phdr.p_type == PT_LOAD && (phdr.p_flags & PF_X))
            {
                unw_word_t low = info->dlpi_addr + phdr.p_vaddr;
                unw_word_t high = low + phdr.p_memsz;
                if (!foundExecSeg || low < segLow)
                    segLow = low;
                if (!foundExecSeg || high > segHigh)
                    segHigh = high;
                foundExecSeg = true;
            }
        }

        if (!foundExecSeg || segLow == segHigh)
            continue;

        int fd = open(modulePath, O_RDONLY | O_CLOEXEC);
        if (fd < 0)
            continue;

        struct stat st;
        if (fstat(fd, &st) < 0 || st.st_size < static_cast<off_t>(sizeof(ElfW(Ehdr))))
        {
            close(fd);
            continue;
        }

        auto fileSize = static_cast<size_t>(st.st_size);
        ScopedMmap mapping(fd, fileSize);
        if (!mapping)
            continue;

        auto* ehdr = static_cast<const ElfW(Ehdr)*>(mapping.data);
        if (memcmp(ehdr->e_ident, ELFMAG, SELFMAG) != 0)
            continue;

        unw_word_t loadOffset = info->dlpi_addr;

        auto base = static_cast<const uint8_t*>(mapping.data);

        if (ehdr->e_shoff == 0 || ehdr->e_shnum == 0 ||
            ehdr->e_shoff + static_cast<size_t>(ehdr->e_shnum) * sizeof(ElfW(Shdr)) > fileSize)
            continue;

        auto* shdrs = reinterpret_cast<const ElfW(Shdr)*>(base + ehdr->e_shoff);

        moduleSymbols.clear();

        for (int s = 0; s < ehdr->e_shnum; ++s)
        {
            if (shdrs[s].sh_type != SHT_SYMTAB && shdrs[s].sh_type != SHT_DYNSYM)
                continue;

            if (shdrs[s].sh_entsize == 0)
                continue;

            if (shdrs[s].sh_offset + shdrs[s].sh_size > fileSize)
                continue;

            auto symCount = shdrs[s].sh_size / shdrs[s].sh_entsize;
            auto* syms = reinterpret_cast<const ElfW(Sym)*>(base + shdrs[s].sh_offset);

            for (size_t j = 0; j < symCount; ++j)
            {
                if (ELF64_ST_TYPE(syms[j].st_info) != STT_FUNC)
                    continue;
                if (syms[j].st_shndx == SHN_UNDEF)
                    continue;
                if (syms[j].st_size == 0)
                    continue;

                unw_word_t startIp = syms[j].st_value + loadOffset;
                unw_word_t endIp = startIp + syms[j].st_size;
                moduleSymbols.push_back({startIp, endIp});
            }
        }

        if (moduleSymbols.empty())
            continue;

        std::sort(moduleSymbols.begin(), moduleSymbols.end(),
                  [](const AbsFuncEntry& a, const AbsFuncEntry& b) {
                      return a.start_ip < b.start_ip || (a.start_ip == b.start_ip && a.end_ip < b.end_ip);
                  });

        auto last = std::unique(moduleSymbols.begin(), moduleSymbols.end(),
                                [](const AbsFuncEntry& a, const AbsFuncEntry& b) {
                                    return a.start_ip == b.start_ip && a.end_ip == b.end_ip;
                                });
        moduleSymbols.erase(last, moduleSymbols.end());

        auto symOffset = static_cast<uint32_t>(outSymbols.size());
        auto symCount = static_cast<uint32_t>(moduleSymbols.size());
        outRegions.push_back({segLow, segHigh, symOffset, symCount});

        for (const auto& abs : moduleSymbols)
        {
            outSymbols.push_back({
                static_cast<uint32_t>(abs.start_ip - segLow),
                static_cast<uint32_t>(abs.end_ip - abs.start_ip)});
        }
    }

    std::sort(outRegions.begin(), outRegions.end(),
              [](const ModuleRegion& a, const ModuleRegion& b) { return a.addr_low < b.addr_low; });

    Log::Debug("LibrariesInfoCache: Symbol cache built with ", outRegions.size(),
               " modules and ", outSymbols.size(), " function entries.");
}

int LibrariesInfoCache::FindFunctionStart(unw_word_t ip, unw_word_t* func_start) const
{
    auto regionIt = std::upper_bound(
        _moduleRegions.begin(), _moduleRegions.end(), ip,
        [](unw_word_t addr, const ModuleRegion& region) { return addr < region.addr_low; });

    if (regionIt == _moduleRegions.begin())
        return -UNW_ENOINFO;

    --regionIt;
    if (ip >= regionIt->addr_high)
        return -UNW_ENOINFO;

    const FuncEntry* symBegin = _symbols.data() + regionIt->sym_offset;
    const FuncEntry* symEnd = symBegin + regionIt->sym_count;
    unw_word_t baseLow = regionIt->addr_low;

    auto symIt = std::upper_bound(
        symBegin, symEnd, ip,
        [baseLow](unw_word_t addr, const FuncEntry& entry) {
            return addr < baseLow + entry.offset;
        });

    if (symIt == symBegin)
        return -UNW_ENOINFO;

    --symIt;
    unw_word_t startIp = baseLow + symIt->offset;
    if (ip >= startIp + symIt->size)
        return -UNW_ENOINFO;

    *func_start = startIp;
    return 0;
}

int LibrariesInfoCache::GetProcName(unw_addr_space_t as, unw_word_t ip,
                                    char* buf, size_t buf_len,
                                    unw_word_t* offp, void* arg)
{
    auto* instance = s_instance.load(std::memory_order_acquire);
    if (instance == nullptr)
        return -UNW_ENOINFO;
    return instance->GetProcNameImpl(as, ip, buf, buf_len, offp, arg);
}

int LibrariesInfoCache::GetProcNameImpl(unw_addr_space_t /*as*/, unw_word_t ip,
                                        char* buf, size_t /*buf_len*/,
                                        unw_word_t* offp, void* /*arg*/)
{
    std::shared_lock lock(_cacheLock);
    unw_word_t func_start;
    if (FindFunctionStart(ip, &func_start) != 0)
        return -UNW_ENOINFO;

    *offp = ip - func_start;
    buf[0] = '\0';
    return 0;
}
#endif

int LibrariesInfoCache::DlIteratePhdr(unw_iterate_phdr_callback_t callback, void* data)
{
    auto* instance = s_instance.load(std::memory_order_acquire);
    if (instance == nullptr)
    {
        return 0;
    }

    return instance->DlIteratePhdrImpl(callback, data);
}

int LibrariesInfoCache::DlIteratePhdrImpl(unw_iterate_phdr_callback_t callback, void* data)
{
    std::shared_lock l(_cacheLock);

    int rc = 0;
    for (auto& wrappedInfo : _librariesInfo)
    {
        auto [info, size] = wrappedInfo.Get();
        rc = callback(info, size, data);
        if (rc != 0)
        {
            break;
        }
    }
    return rc;
}

void LibrariesInfoCache::NotifyCacheUpdate()
{
    auto* instance = s_instance.load(std::memory_order_acquire);
    if (instance == nullptr)
    {
        return;
    }
    instance->NotifyCacheUpdateImpl();
}

void LibrariesInfoCache::NotifyCacheUpdateImpl()
{
    _event.Set();
}

#ifdef DD_TEST
void* LibrariesInfoCache::GetLocalAddressSpace()
{
    return unw_local_addr_space;
}
#endif

// --------------------------------------------------------------------------
// FootprintTracker method implementations
// --------------------------------------------------------------------------

void FootprintTracker::RegisterMetrics(MetricsRegistry& registry)
{
    libCountMetric = registry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_count", [this]() {
        return static_cast<double_t>(libCount.load(std::memory_order_relaxed));
    });
    memoryFootprintMetric = registry.GetOrRegister<ProxyMetric>("dotnet_memory_footprint_libs_cache", [this]() {
        return static_cast<double_t>(trackingResource.GetCurrentUsage());
    });
#ifdef ARM64
    moduleCountMetric = registry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_module_regions", [this]() {
        return static_cast<double_t>(moduleRegionCount.load(std::memory_order_relaxed));
    });
    symbolCountMetric = registry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_symbols", [this]() {
        return static_cast<double_t>(symbolCount.load(std::memory_order_relaxed));
    });
#endif
    updateCpuMetric = registry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_update_cpu_ns");
    reloadDurationMetric = registry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_reload_duration_ns");
    lockHoldDurationMetric = registry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_lock_hold_duration_ns");
}

void FootprintTracker::LogStats(std::size_t libCount)
{
    Log::Info("LibrariesInfoCache stats:");
    Log::Info("  Cache reloads: ", reloadCount);
    Log::Info("  Total CPU (worker): ", totalCpuNs / 1'000'000, " ms");
    Log::Info("  Libraries in cache: ", libCount);
#ifdef ARM64
    Log::Info("  Module regions: ", moduleRegionCount.load(std::memory_order_relaxed));
    Log::Info("  Symbol entries: ", symbolCount.load(std::memory_order_relaxed));
#endif
    Log::Info("  Memory current: ", trackingResource.GetCurrentUsage(), " bytes");
}

void FootprintTracker::RecordReload(std::chrono::steady_clock::duration reloadDuration,
                                    std::chrono::steady_clock::duration lockDuration,
                                    struct timespec cpuStart, struct timespec cpuEnd)
{
    auto cpuNs = static_cast<double_t>(
        (cpuEnd.tv_sec - cpuStart.tv_sec) * 1'000'000'000LL + (cpuEnd.tv_nsec - cpuStart.tv_nsec));

    totalCpuNs += static_cast<std::uint64_t>(cpuNs);

    updateCpuMetric->Add(cpuNs);
    reloadDurationMetric->Add(static_cast<double_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(reloadDuration).count()));
    lockHoldDurationMetric->Add(static_cast<double_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(lockDuration).count()));
}
