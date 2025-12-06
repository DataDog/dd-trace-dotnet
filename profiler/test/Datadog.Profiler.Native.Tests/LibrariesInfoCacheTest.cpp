// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "MemoryResourceManager.h"
#include "profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/LibrariesInfoCache.h"

#include <string>
#include <thread>
#include <chrono>

using namespace std::chrono_literals;

// This test is mainly for ASAN & UBSAN.
// I want to be checked if we leak memory or we failed at implemented it correctly
// For that we need to use the default memory resource (new/delete)
TEST(LibrariesInfoCacheTests, MakeSureWeDoNotLeakMemory)
{
    auto cache = LibrariesInfoCache(MemoryResourceManager::GetDefault());

    cache.Start();

    for (auto i = 0; i < 10; i++)
    {
        cache.NotifyCacheUpdateImpl();
        std::this_thread::sleep_for(100ms);
    }

    cache.Stop();
}

TEST(LibrariesInfoCacheTests, DetectsMemfdMappingsAsManaged)
{
    EXPECT_TRUE(LibrariesInfoCache::ShouldTreatAsManagedMapping("", 0));
    EXPECT_TRUE(LibrariesInfoCache::ShouldTreatAsManagedMapping("/memfd:doublemapper (deleted)", 11344));
    EXPECT_FALSE(
        LibrariesInfoCache::ShouldTreatAsManagedMapping(
            "/usr/share/dotnet/shared/Microsoft.NETCore.App/10.0.0-rc.2.25502.107/libcoreclr.so",
            29086666));
}