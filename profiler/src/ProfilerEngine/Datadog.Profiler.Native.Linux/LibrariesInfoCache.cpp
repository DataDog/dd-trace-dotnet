// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LibrariesInfoCache.h"

#include "Log.h"
#include "OpSysTools.h"

#include <algorithm>
#include <elf.h>
#include <fcntl.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <signal.h>
#include <sys/syscall.h>
#include <time.h>
#include <unistd.h>

using namespace std::chrono_literals;

namespace {

constexpr auto CpuTimerInterval = 10ms;

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

std::atomic<LibrariesInfoCache*> LibrariesInfoCache::s_instance{nullptr};

extern "C" void (*volatile dd_notify_libraries_cache_update)() __attribute__((weak));

namespace {
std::atomic<std::uint64_t>* s_cpuTicksPtr = nullptr;

void CpuTickSignalHandler(int /*sig*/)
{
    auto* ptr = s_cpuTicksPtr;
    if (ptr != nullptr)
    {
        ptr->fetch_add(1, std::memory_order_relaxed);
    }
}
} // anonymous namespace

LibrariesInfoCache::LibrariesInfoCache(shared::pmr::memory_resource* resource, MetricsRegistry& metricsRegistry) :
    _trackingResource{resource},
    _wrappersAllocator{&_trackingResource},
    _librariesInfo{&_trackingResource},
    _newCache{&_trackingResource},
#ifdef ARM64
    _moduleRegions{&_trackingResource},
    _symbols{&_trackingResource},
    _newRegions{&_trackingResource},
    _newSymbols{&_trackingResource},
#endif
    _stopRequested{false},
    _event(true) // set the event to force updating the cache the first time Wait is called
{
    _libCountMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_count", [this]() {
        return static_cast<double_t>(_librariesInfo.size());
    });
    _memoryFootprintMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_memory_footprint_libs_cache", [this]() {
        return static_cast<double_t>(_trackingResource.GetCurrentUsage());
    });
    _memoryPeakMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_memory_peak", [this]() {
        return static_cast<double_t>(_trackingResource.GetPeakUsage());
    });
    _cpuTicksMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_cpu_ticks", [this]() {
        return static_cast<double_t>(_cpuTicks.load(std::memory_order_relaxed));
    });
#ifdef ARM64
    _moduleCountMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_module_regions", [this]() {
        return static_cast<double_t>(_moduleRegions.size());
    });
    _symbolCountMetric = metricsRegistry.GetOrRegister<ProxyMetric>("dotnet_libs_cache_symbols", [this]() {
        return static_cast<double_t>(_symbols.size());
    });
#endif
    _updateCpuMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_update_cpu_ns");
    _reloadDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_reload_duration_ns");
    _lockHoldDurationMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_lock_hold_duration_ns");
    _reloadAllocationsMetric = metricsRegistry.GetOrRegister<MeanMaxMetric>("dotnet_libs_cache_reload_allocations");
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

    LogStats();
    _librariesInfo.clear();
#ifdef ARM64
    _moduleRegions.clear();
    _symbols.clear();
#endif

    return true;
}

void LibrariesInfoCache::LogStats()
{
    if (!Log::IsDebugEnabled())
    {
        return;
    }

    auto cpuTime = CpuTimerInterval * _cpuTicks.load(std::memory_order_relaxed);
    auto avgReloadDuration = _reloadCount > 0
        ? std::chrono::duration_cast<std::chrono::microseconds>(_totalReloadDuration) / _reloadCount
        : std::chrono::microseconds{0};
    auto maxReloadDuration = std::chrono::duration_cast<std::chrono::microseconds>(_maxReloadDuration);
    auto avgLockDuration = _reloadCount > 0
        ? std::chrono::duration_cast<std::chrono::microseconds>(_totalLockHoldDuration) / _reloadCount
        : std::chrono::microseconds{0};
    auto maxLockDuration = std::chrono::duration_cast<std::chrono::microseconds>(_maxLockHoldDuration);

    Log::Info("LibrariesInfoCache stats:");
    Log::Info("  CPU time (worker thread): ", cpuTime);
    Log::Info("  Notifications received: ", _notificationCount);
    Log::Info("  Cache reloads: ", _reloadCount);
    Log::Info("  Reload duration avg: ", avgReloadDuration, ", max: ", maxReloadDuration);
    Log::Info("  Lock hold duration avg: ", avgLockDuration, ", max: ", maxLockDuration);
    Log::Info("  Libraries in cache: ", _librariesInfo.size());
    Log::Info("  Memory current: ", _trackingResource.GetCurrentUsage(), " bytes");
    Log::Info("  Memory peak: ", _trackingResource.GetPeakUsage(), " bytes");
    Log::Info("  Memory total allocated: ", _trackingResource.GetTotalAllocated(), " bytes");
    Log::Info("  Memory total deallocated: ", _trackingResource.GetTotalDeallocated(), " bytes");
    Log::Info("  Memory allocation count: ", _trackingResource.GetAllocationCount());
}

void LibrariesInfoCache::SetupCpuTimer()
{
    const int cpuTimerSignal = SIGRTMIN + 10;

    struct sigaction sa = {};
    sa.sa_handler = CpuTickSignalHandler;
    sa.sa_flags = SA_RESTART;
    sigemptyset(&sa.sa_mask);
    if (sigaction(cpuTimerSignal, &sa, nullptr) != 0)
    {
        Log::Warn("LibrariesInfoCache: Failed to install signal handler (signal=", cpuTimerSignal, ") for CPU measurement: ", strerror(errno));
        return;
    }

    s_cpuTicksPtr = &_cpuTicks;

    auto tid = static_cast<int>(syscall(SYS_gettid));

    struct sigevent sev = {};
    sev.sigev_signo = cpuTimerSignal;
    sev.sigev_notify = SIGEV_THREAD_ID;
    ((int*)&sev.sigev_notify)[1] = tid;

    clockid_t clock = ((~tid) << 3) | 6; // CPUCLOCK_SCHED | CPUCLOCK_PERTHREAD_MASK
    if (syscall(__NR_timer_create, clock, &sev, &_cpuTimerId) < 0)
    {
        Log::Warn("LibrariesInfoCache: Failed to create CPU timer: ", strerror(errno));
        s_cpuTicksPtr = nullptr;
        return;
    }

    _cpuTimerCreated = true;

    struct itimerspec its = {};

    constexpr auto cpuTimerIntervalNs = std::chrono::duration_cast<std::chrono::nanoseconds>(CpuTimerInterval).count();
    its.it_interval = {0, cpuTimerIntervalNs};
    its.it_value = {0, cpuTimerIntervalNs};
    if (syscall(__NR_timer_settime, _cpuTimerId, 0, &its, nullptr) < 0)
    {
        Log::Warn("LibrariesInfoCache: Failed to arm CPU timer: ", strerror(errno));
        syscall(__NR_timer_delete, _cpuTimerId);
        _cpuTimerCreated = false;
        s_cpuTicksPtr = nullptr;
        return;
    }

    Log::Info("LibrariesInfoCache: CPU timer armed on worker thread (tid=", tid, ")");
}

void LibrariesInfoCache::TeardownCpuTimer()
{
    if (_cpuTimerCreated)
    {
        syscall(__NR_timer_delete, _cpuTimerId);
        _cpuTimerCreated = false;
    }
    s_cpuTicksPtr = nullptr;
}

void LibrariesInfoCache::Work(std::shared_ptr<AutoResetEvent> startEvent)
{
    OpSysTools::SetNativeThreadName(WStr("DD_LibsCache"));
    SetupCpuTimer();

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

    TeardownCpuTimer();
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

    _reloadCount++;
    _trackingResource.ResetPerReloadStats();
    auto reloadStart = std::chrono::steady_clock::now();

    // use to measure how much CPU ONE reload is consuming
    struct timespec cpuStart = {};
    clock_gettime(CLOCK_THREAD_CPUTIME_ID, &cpuStart);

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

    auto lockStart = std::chrono::steady_clock::now();
    {
        std::unique_lock l{_cacheLock};
        _librariesInfo.swap(_newCache);
#ifdef ARM64
        _moduleRegions.swap(_newRegions);
        _symbols.swap(_newSymbols);
#endif
    }
    auto lockEnd = std::chrono::steady_clock::now();

    _newCache.clear();
#ifdef ARM64
    _newRegions.clear();
    _newSymbols.clear();
#endif

    auto reloadEnd = std::chrono::steady_clock::now();
    auto reloadDuration = reloadEnd - reloadStart;
    auto lockDuration = lockEnd - lockStart;

    _totalReloadDuration += reloadDuration;
    _totalLockHoldDuration += lockDuration;
    if (reloadDuration > _maxReloadDuration)
    {
        _maxReloadDuration = reloadDuration;
    }
    if (lockDuration > _maxLockHoldDuration)
    {
        _maxLockHoldDuration = lockDuration;
    }

    struct timespec cpuEnd = {};
    clock_gettime(CLOCK_THREAD_CPUTIME_ID, &cpuEnd);
    auto cpuNs = static_cast<double_t>(
        (cpuEnd.tv_sec - cpuStart.tv_sec) * 1'000'000'000LL + (cpuEnd.tv_nsec - cpuStart.tv_nsec));

    _updateCpuMetric->Add(cpuNs);
    _reloadDurationMetric->Add(static_cast<double_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(reloadDuration).count()));
    _lockHoldDurationMetric->Add(static_cast<double_t>(
        std::chrono::duration_cast<std::chrono::nanoseconds>(lockDuration).count()));
    _reloadAllocationsMetric->Add(static_cast<double_t>(_trackingResource.GetReloadAllocations()));
}

#ifdef ARM64
void LibrariesInfoCache::BuildSymbolCache(
    std::vector<DlPhdrInfoWrapper, shared::pmr::polymorphic_allocator<DlPhdrInfoWrapper>>& phdrCache,
    std::vector<ModuleRegion, shared::pmr::polymorphic_allocator<ModuleRegion>>& outRegions,
    std::vector<FuncEntry, shared::pmr::polymorphic_allocator<FuncEntry>>& outSymbols)
{
    std::vector<FuncEntry> moduleSymbols;

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
                  [](const FuncEntry& a, const FuncEntry& b) {
                      return a.start_ip < b.start_ip || (a.start_ip == b.start_ip && a.end_ip < b.end_ip);
                  });

        // Remove duplicates
        auto last = std::unique(moduleSymbols.begin(), moduleSymbols.end(),
                                [](const FuncEntry& a, const FuncEntry& b) {
                                    return a.start_ip == b.start_ip && a.end_ip == b.end_ip;
                                });
        moduleSymbols.erase(last, moduleSymbols.end());

        auto symOffset = static_cast<uint32_t>(outSymbols.size());
        auto symCount = static_cast<uint32_t>(moduleSymbols.size());
        outRegions.push_back({segLow, segHigh, symOffset, symCount});
        outSymbols.insert(outSymbols.end(), moduleSymbols.begin(), moduleSymbols.end());
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

    auto symIt = std::upper_bound(
        symBegin, symEnd, ip,
        [](unw_word_t addr, const FuncEntry& entry) { return addr < entry.start_ip; });

    if (symIt == symBegin)
        return -UNW_ENOINFO;

    --symIt;
    if (ip >= symIt->end_ip)
        return -UNW_ENOINFO;

    *func_start = symIt->start_ip;
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
    _notificationCount++;
    _event.Set();
}

#ifdef DD_TEST
void* LibrariesInfoCache::GetLocalAddressSpace()
{
    return unw_local_addr_space;
}
#endif