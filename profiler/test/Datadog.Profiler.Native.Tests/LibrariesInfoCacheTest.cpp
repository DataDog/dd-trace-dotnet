// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "MemoryResourceManager.h"
#include "LibrariesInfoCache.h"

#include "Backtrace2Unwinder.h"

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