// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CoreLibMockProfilerInfo.h"
#include "CoreLibModuleProvider.h"

namespace
{
constexpr ModuleID ApplicationModuleId = 7;
constexpr ModuleID CoreLibModuleId = 42;
} // namespace

TEST(CoreLibModuleProviderTest, IgnoresModulesOtherThanTheCoreLibrary)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.AddModule(ApplicationModuleId, WStr("MyApplication"));

    CoreLibModuleProvider provider(&profilerInfo);

    ASSERT_FALSE(provider.OnModuleLoaded(ApplicationModuleId));
    ASSERT_EQ(static_cast<ModuleID>(0), provider.GetModuleId());
}

TEST(CoreLibModuleProviderTest, IgnoresModulesWithoutAssemblyName)
{
    CoreLibMockProfilerInfo profilerInfo;

    CoreLibModuleProvider provider(&profilerInfo);

    // the module is unknown to the mock: getting its assembly name fails
    ASSERT_FALSE(provider.OnModuleLoaded(ApplicationModuleId));
    ASSERT_EQ(static_cast<ModuleID>(0), provider.GetModuleId());
}

TEST(CoreLibModuleProviderTest, CapturesTheCoreLibraryModuleOnlyOnce)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.AddModule(ApplicationModuleId, WStr("MyApplication"));
    profilerInfo.AddModule(CoreLibModuleId, WStr("System.Private.CoreLib"));
    profilerInfo.AddModule(CoreLibModuleId + 1, WStr("System.Private.CoreLib"));

    CoreLibModuleProvider provider(&profilerInfo);

    ASSERT_FALSE(provider.OnModuleLoaded(ApplicationModuleId));
    ASSERT_TRUE(provider.OnModuleLoaded(CoreLibModuleId));
    ASSERT_EQ(CoreLibModuleId, provider.GetModuleId());

    // a second core library never replaces the first one
    ASSERT_FALSE(provider.OnModuleLoaded(CoreLibModuleId));
    ASSERT_FALSE(provider.OnModuleLoaded(CoreLibModuleId + 1));
    ASSERT_EQ(CoreLibModuleId, provider.GetModuleId());
}

TEST(CoreLibModuleProviderTest, DetectsMscorlibForNetFramework)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.AddModule(CoreLibModuleId, WStr("mscorlib"));

    CoreLibModuleProvider provider(&profilerInfo);

    ASSERT_TRUE(provider.OnModuleLoaded(CoreLibModuleId));
    ASSERT_EQ(CoreLibModuleId, provider.GetModuleId());
}

TEST(CoreLibModuleProviderTest, ResolveTypeDoesNothingBeforeTheCoreLibraryIsLoaded)
{
    CoreLibMockProfilerInfo profilerInfo;

    CoreLibModuleProvider provider(&profilerInfo);

    ASSERT_EQ(static_cast<ClassID>(0), provider.ResolveTypeInCoreLib(WStr("System.Int32")));
    ASSERT_TRUE(profilerInfo.MetadataRequests.empty());
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
}

TEST(CoreLibModuleProviderTest, GetMetadataFailsBeforeTheCoreLibraryIsLoaded)
{
    CoreLibMockProfilerInfo profilerInfo;

    CoreLibModuleProvider provider(&profilerInfo);

    ASSERT_FALSE(provider.GetMetadata());
    ASSERT_TRUE(profilerInfo.MetadataRequests.empty());
}

TEST(CoreLibModuleProviderTest, ResolveTypeRetriesAfterMetadataFailure)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.AddModule(CoreLibModuleId, WStr("System.Private.CoreLib"));

    CoreLibModuleProvider provider(&profilerInfo);
    ASSERT_TRUE(provider.OnModuleLoaded(CoreLibModuleId));

    // the mock has no metadata to offer: the resolution fails but must have been
    // attempted on the core library module
    ASSERT_EQ(static_cast<ClassID>(0), provider.ResolveTypeInCoreLib(WStr("System.Int32")));
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.MetadataRequests.size());
    ASSERT_EQ(CoreLibModuleId, profilerInfo.MetadataRequests[0]);

    // A failed lookup is not cached: metadata may become available later.
    ASSERT_EQ(static_cast<ClassID>(0), provider.ResolveTypeInCoreLib(WStr("System.Int32")));
    ASSERT_EQ(static_cast<size_t>(2), profilerInfo.MetadataRequests.size());
    ASSERT_EQ(CoreLibModuleId, profilerInfo.MetadataRequests[1]);
}
