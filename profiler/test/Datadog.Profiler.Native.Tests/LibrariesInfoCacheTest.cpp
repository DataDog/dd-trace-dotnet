// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "MemoryResourceManager.h"
#include "LibrariesInfoCache.h"

#include <cstdlib>

#ifndef ARM64
#include "Backtrace2Unwinder.h"
#endif

struct ServiceWrapper
{
    ServiceWrapper(ServiceBase* service) : _service(service) {
        _service->Start();
    }
    ~ServiceWrapper() {
        _service->Stop();
    }
    ServiceBase* _service;
};

// This test is mainly for ASAN & UBSAN.
// I want to be checked if we leak memory or we failed at implemented it correctly
// For that we need to use the default memory resource (new/delete)
TEST(LibrariesInfoCacheTests, MakeSureWeDoNotLeakMemory)
{
    auto cache = LibrariesInfoCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&cache);

    for(auto i = 0; i < 10; i++)
    {
        cache.NotifyCacheUpdateImpl();
        std::this_thread::sleep_for(100ms);
    }
}

TEST(LibrariesInfoCacheTests, MakeSureWeUseTheCorrectAddressSpace)
{
#ifdef ARM64
    auto local_addr_space = _ULaarch64_local_addr_space;
#else
    auto local_addr_space = _ULx86_64_local_addr_space;
#endif
    ASSERT_EQ(local_addr_space, LibrariesInfoCache::GetLocalAddressSpace());
}

TEST(LibrariesInfoCacheTests, CheckBehaviorAgainstDlIteratePhdr)
{
    struct Info{
        std::uintptr_t addr_base;
        std::size_t size;
    };

    std::vector<Info> cache;

    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* cache = static_cast<std::vector<Info>*>(data);
            cache->emplace_back(Info{.addr_base = info->dlpi_addr, .size = size});
            return 0;
        },
        &cache);

    std::vector<Info> cache2;
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);
    LibrariesInfoCache::DlIteratePhdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* cache = static_cast<std::vector<Info>*>(data);
            cache->emplace_back(Info{.addr_base = info->dlpi_addr, .size = size});
            return 0;
        },
        &cache2);

    std::sort(cache.begin(), cache.end(), [](const Info& a, const Info& b) {
        return a.addr_base < b.addr_base;
    });
    std::sort(cache2.begin(), cache2.end(), [](const Info& a, const Info& b) {
        return a.addr_base < b.addr_base;
    });
    ASSERT_EQ(cache.size(), cache2.size());

    for(auto i = 0; i < cache.size(); i++)
    {
        ASSERT_EQ(cache[i].addr_base, cache2[i].addr_base);
        ASSERT_EQ(cache[i].size, cache2[i].size);
    }
}

#ifdef ARM64
__attribute__((noinline)) void KnownTestFunction_ForGetProcNameTest()
{
    asm volatile("");
}

__attribute__((noinline)) int KnownTestFunction_Second(int x)
{
    asm volatile("");
    return x + 1;
}

__attribute__((noinline)) int KnownTestFunction_Third(int x, int y)
{
    asm volatile("");
    return x * y;
}

TEST(LibrariesInfoCacheTests, GetProcNameReturnsCorrectOffsetForKnownFunction)
{
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);

    auto ip = reinterpret_cast<unw_word_t>(&KnownTestFunction_ForGetProcNameTest);

    char buf[128] = {};
    unw_word_t offp = 0;

    auto* as = static_cast<unw_addr_space_t>(LibrariesInfoCache::GetLocalAddressSpace());
    int rc = LibrariesInfoCache::GetProcName(as, ip, buf, sizeof(buf), &offp, nullptr);

    ASSERT_EQ(rc, 0) << "GetProcName failed for a known function address";
    ASSERT_EQ(offp, 0u);
}

TEST(LibrariesInfoCacheTests, GetProcNameReturnsCorrectOffsetForMultipleKnownFunctions)
{
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);

    auto* as = static_cast<unw_addr_space_t>(LibrariesInfoCache::GetLocalAddressSpace());

    // Test functions from this binary (noinline, guaranteed symbol table entries)
    // plus qsort from libc: pure C on both glibc and musl, no IFUNC, no assembly,
    // always has st_size > 0 — unlike strlen/memcpy which are IFUNC-dispatched on
    // ARM64 glibc with st_size == 0 assembly implementations.
    std::vector<unw_word_t> testIps = {
        reinterpret_cast<unw_word_t>(&KnownTestFunction_ForGetProcNameTest),
        reinterpret_cast<unw_word_t>(&KnownTestFunction_Second),
        reinterpret_cast<unw_word_t>(&KnownTestFunction_Third),
        reinterpret_cast<unw_word_t>(&qsort),
    };

    for (auto ip : testIps)
    {
        char buf[128] = {};
        unw_word_t offp = 0;
        int rc = LibrariesInfoCache::GetProcName(as, ip, buf, sizeof(buf), &offp, nullptr);

        ASSERT_EQ(rc, 0) << "GetProcName failed for ip=0x" << std::hex << ip;
        ASSERT_EQ(offp, 0u) << "Offset at entry should be 0 for ip=0x" << std::hex << ip;
    }
}

TEST(LibrariesInfoCacheTests, GetProcNameReturnsOffsetForAddressInsideFunction)
{
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);

    auto funcAddr = reinterpret_cast<unw_word_t>(&KnownTestFunction_Third);
    auto ip = funcAddr + 4;

    char buf[128] = {};
    unw_word_t offp = 0;

    auto* as = static_cast<unw_addr_space_t>(LibrariesInfoCache::GetLocalAddressSpace());
    int rc = LibrariesInfoCache::GetProcName(as, ip, buf, sizeof(buf), &offp, nullptr);

    ASSERT_EQ(rc, 0) << "GetProcName failed for address inside KnownTestFunction_Third";
    ASSERT_EQ(offp, 4u) << "Offset should reflect distance from function start";
}

TEST(LibrariesInfoCacheTests, GetProcNameFailsForBogusAddress)
{
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);

    unw_word_t ip = 0x1;

    char buf[128] = {};
    unw_word_t offp = 0;

    auto* as = static_cast<unw_addr_space_t>(LibrariesInfoCache::GetLocalAddressSpace());
    int rc = LibrariesInfoCache::GetProcName(as, ip, buf, sizeof(buf), &offp, nullptr);

    ASSERT_NE(rc, 0) << "GetProcName should fail for an unmapped address";
}

TEST(LibrariesInfoCacheTests, GetProcNameReplacesAndRestoresOriginalAccessor)
{
    auto* as = static_cast<unw_addr_space_t>(LibrariesInfoCache::GetLocalAddressSpace());
    unw_accessors_t* acc = unw_get_accessors(as);

    auto originalGetProcName = acc->get_proc_name;

    {
        LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
        ServiceWrapper serviceWrapper(&libCache);

        ASSERT_EQ(acc->get_proc_name, &LibrariesInfoCache::GetProcName)
            << "get_proc_name should point to LibrariesInfoCache::GetProcName after Start";
        ASSERT_NE(acc->get_proc_name, originalGetProcName)
            << "get_proc_name should differ from the original after Start";
    }

    ASSERT_EQ(acc->get_proc_name, originalGetProcName)
        << "get_proc_name should be restored to the original after Stop";
}

TEST(LibrariesInfoCacheTests, BuildSymbolCacheProducesNoDuplicates)
{
    LibrariesInfoCache libCache(MemoryResourceManager::GetDefault());
    ServiceWrapper serviceWrapper(&libCache);

    std::vector<DlPhdrInfoWrapper> phdrCache;
    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t size, void* data) {
            auto* cache = static_cast<std::vector<DlPhdrInfoWrapper>*>(data);
            cache->emplace_back(info, size);
            return 0;
        },
        &phdrCache);

    std::vector<ModuleRegion> regions;
    std::vector<FuncEntry> symbols;
    libCache.BuildSymbolCache(phdrCache, regions, symbols);

    ASSERT_FALSE(symbols.empty()) << "Expected at least some symbols from the test binary";

    for (size_t r = 0; r < regions.size(); ++r)
    {
        auto& region = regions[r];
        for (uint32_t i = 1; i < region.sym_count; ++i)
        {
            auto idx = region.sym_offset + i;
            auto prev = region.sym_offset + i - 1;
            bool isDuplicate = symbols[idx].start_ip == symbols[prev].start_ip &&
                               symbols[idx].end_ip == symbols[prev].end_ip;
            EXPECT_FALSE(isDuplicate)
                << "Duplicate symbol entry in region " << r
                << " at index " << i
                << ": start_ip=0x" << std::hex << symbols[idx].start_ip
                << " end_ip=0x" << symbols[idx].end_ip;
        }
    }
}
#endif