// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "CoreLibMockProfilerInfo.h"
#include "CoreLibModuleProvider.h"
#include "InlineVTCache.h"

// These tests cover when and how a type is inspected to find its inline value types.
//
// A type is inspected by resolving the metadata tokens of its fields signatures, which means
// asking the runtime to load types. This is forbidden while it is suspended for the heap dump
// GC (it throws EETypeLoadException or crashes in the type loader), so no type may be inspected
// during a traversal.
//
// On top of that, GetClassFromTokenAndTypeArgs only accepts a TypeDef defined in the module it
// is given: any other kind of token would start a type load that always fails.

namespace
{
constexpr ModuleID ApplicationModuleId = 7;
constexpr ModuleID CoreLibModuleId = 42;

constexpr mdToken TypeDefToken = 0x02000005;  // mdtTypeDef
constexpr mdToken TypeRefToken = 0x010000b8;  // mdtTypeRef: the one that used to crash
constexpr mdToken TypeSpecToken = 0x1b000001; // mdtTypeSpec

// InlineVTCache only needs the ICorProfilerInfo4 part of the interface for these tests.
InlineVTCache CreateCache(CoreLibMockProfilerInfo& profilerInfo, CoreLibModuleProvider* pProvider)
{
    auto* pInfo = reinterpret_cast<ICorProfilerInfo12*>(static_cast<ICorProfilerInfo4*>(&profilerInfo));
    return InlineVTCache(pInfo, pProvider);
}

// A ClassID is the address of the MethodTable of the type: the cache reads its first DWORD to
// know whether the type contains references, so a fake one is enough for these tests.
class FakeMethodTable
{
public:
    FakeMethodTable() :
        _flags(GCDesc::Flag_ContainsPointers)
    {
    }

    ClassID GetClassID() const
    {
        return reinterpret_cast<ClassID>(&_flags);
    }

    // Stands in for "this address no longer describes what it described during the dump".
    // A freed MethodTable cannot be simulated (reading one is undefined), so the flag the
    // cache used to read first is simply turned off.
    void ClearContainsPointers()
    {
        _flags = 0;
    }

private:
    uint32_t _flags;
};
} // namespace

TEST(InlineVTCacheTest, TypeRefTokenIsNeverGivenToTheRuntime)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.ClassIdToReturn = 0x1234;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<ClassID>(0), cache.ResolveClassIDFromToken(ApplicationModuleId, TypeRefToken, 0, nullptr));
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
}

TEST(InlineVTCacheTest, TypeSpecTokenIsNeverGivenToTheRuntime)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.ClassIdToReturn = 0x1234;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<ClassID>(0), cache.ResolveClassIDFromToken(ApplicationModuleId, TypeSpecToken, 0, nullptr));
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
}

TEST(InlineVTCacheTest, TypeDefTokenIsResolved)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.ClassIdToReturn = 0x1234;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<ClassID>(0x1234), cache.ResolveClassIDFromToken(ApplicationModuleId, TypeDefToken, 0, nullptr));
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.ResolvedTokens.size());
    ASSERT_EQ(TypeDefToken, profilerInfo.ResolvedTokens[0]);
}

TEST(InlineVTCacheTest, FailedTypeDefResolutionReturnsNoClass)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.ClassIdToReturn = 0x1234;
    profilerInfo.ClassFromTokenResult = E_FAIL;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<ClassID>(0), cache.ResolveClassIDFromToken(ApplicationModuleId, TypeDefToken, 0, nullptr));
}

TEST(InlineVTCacheTest, BoundedSignatureHelpersDecodeValidValues)
{
    constexpr COR_SIGNATURE tokenSignature[] = {0x14}; // TypeDef rid 5
    ULONG tokenIdx = 0;
    mdToken token = mdTokenNil;

    ASSERT_TRUE(InlineVTCache::TryUncompressToken(
        tokenSignature,
        static_cast<ULONG>(sizeof(tokenSignature)),
        tokenIdx,
        token));
    ASSERT_EQ(TypeDefToken, token);
    ASSERT_EQ(static_cast<ULONG>(1), tokenIdx);

    constexpr COR_SIGNATURE dataSignature[] = {0x7f};
    ULONG dataIdx = 0;
    ULONG data = 0;

    ASSERT_TRUE(InlineVTCache::TryUncompressData(
        dataSignature,
        static_cast<ULONG>(sizeof(dataSignature)),
        dataIdx,
        data));
    ASSERT_EQ(static_cast<ULONG>(0x7f), data);
    ASSERT_EQ(static_cast<ULONG>(1), dataIdx);
}

TEST(InlineVTCacheTest, BoundedSignatureHelpersRejectTruncatedValues)
{
    constexpr COR_SIGNATURE truncatedTwoByte[] = {0x80};
    ULONG tokenIdx = 0;
    mdToken token = mdTokenNil;

    ASSERT_FALSE(InlineVTCache::TryUncompressToken(
        truncatedTwoByte,
        static_cast<ULONG>(sizeof(truncatedTwoByte)),
        tokenIdx,
        token));
    ASSERT_EQ(static_cast<ULONG>(0), tokenIdx);

    constexpr COR_SIGNATURE truncatedFourByte[] = {0xc0, 0x00};
    ULONG dataIdx = 0;
    ULONG data = 0;

    ASSERT_FALSE(InlineVTCache::TryUncompressData(
        truncatedFourByte,
        static_cast<ULONG>(sizeof(truncatedFourByte)),
        dataIdx,
        data));
    ASSERT_EQ(static_cast<ULONG>(0), dataIdx);
}

TEST(InlineVTCacheTest, PrimitiveTypeIsResolvedInTheCoreLibraryModule)
{
    CoreLibMockProfilerInfo profilerInfo;
    profilerInfo.AddModule(CoreLibModuleId, WStr("System.Private.CoreLib"));

    CoreLibModuleProvider provider(&profilerInfo);
    ASSERT_TRUE(provider.OnModuleLoaded(CoreLibModuleId));

    auto cache = CreateCache(profilerInfo, &provider);

    // no metadata for the inspected module: the primitive can only be resolved by
    // the core library provider, and never through a TypeRef of the inspected module
    ASSERT_EQ(static_cast<ClassID>(0), cache.ResolvePrimitiveClassID(ELEMENT_TYPE_I4, ApplicationModuleId, nullptr));
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.MetadataRequests.size());
    ASSERT_EQ(CoreLibModuleId, profilerInfo.MetadataRequests[0]);
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
}

TEST(InlineVTCacheTest, PrimitiveTypeResolutionIsSkippedWithoutCoreLibraryProvider)
{
    CoreLibMockProfilerInfo profilerInfo;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<ClassID>(0), cache.ResolvePrimitiveClassID(ELEMENT_TYPE_I4, ApplicationModuleId, nullptr));
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
}

TEST(InlineVTCacheTest, UnknownTypeIsNotInspectedDuringTraversal)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    // this is what the traversal does: it must not ask anything to the runtime
    ASSERT_EQ(nullptr, cache.GetInlineVTInfo(methodTable.GetClassID()));

    ASSERT_EQ(static_cast<size_t>(0), profilerInfo.InspectedClassCount);
    ASSERT_TRUE(profilerInfo.ResolvedTokens.empty());
    ASSERT_EQ(static_cast<size_t>(1), cache.GetPendingTypeCount());
}

TEST(InlineVTCacheTest, TypeMetSeveralTimesDuringTraversalIsQueuedOnce)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());
    cache.GetInlineVTInfo(methodTable.GetClassID());
    cache.GetInlineVTInfo(methodTable.GetClassID());

    ASSERT_EQ(static_cast<size_t>(1), cache.GetPendingTypeCount());
    ASSERT_EQ(static_cast<size_t>(0), profilerInfo.InspectedClassCount);
}

// Queuing a type also records a "nothing to attribute" cache entry, so the other objects
// of that type stop at the cache lookup instead of probing the queue again for each one
// while the runtime is suspended.
TEST(InlineVTCacheTest, QueuedTypeIsAlsoCachedSoLaterObjectsOnlyCostALookup)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<size_t>(0), cache.GetCachedTypeCount());

    cache.GetInlineVTInfo(methodTable.GetClassID());

    ASSERT_EQ(static_cast<size_t>(1), cache.GetCachedTypeCount());
    ASSERT_EQ(static_cast<size_t>(1), cache.GetPendingTypeCount());

    // The placeholder must not be mistaken for a real verdict: the type is still
    // inspected once the dump is over.
    ASSERT_EQ(static_cast<size_t>(1), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.InspectedClassCount);
}

TEST(InlineVTCacheTest, QueuedTypeIsInspectedAfterTheDump)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());

    ASSERT_EQ(static_cast<size_t>(1), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.InspectedClassCount);
    ASSERT_EQ(static_cast<size_t>(0), cache.GetPendingTypeCount());
}

TEST(InlineVTCacheTest, InspectedTypeIsNotQueuedAgain)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());
    cache.ResolvePendingTypes();

    // the type has no inline VT (the mock does not describe it) but this is now known:
    // the next traversals must not queue it again
    ASSERT_EQ(nullptr, cache.GetInlineVTInfo(methodTable.GetClassID()));
    ASSERT_EQ(static_cast<size_t>(0), cache.GetPendingTypeCount());
    ASSERT_EQ(static_cast<size_t>(0), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.InspectedClassCount);
}

// A type is queued while the runtime is suspended and inspected once it has resumed, by
// which time a collectible AssemblyLoadContext may have freed its MethodTable. Reading
// that MethodTable to decide whether the type is worth inspecting would fault on a thread
// the CLR knows nothing about, so the runtime is asked to vouch for the ClassID first.
TEST(InlineVTCacheTest, DeferredTypeIsValidatedByTheRuntimeBeforeItsMethodTableIsRead)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());

    methodTable.ClearContainsPointers();

    // reaching the runtime means the MethodTable was not what decided to give up
    ASSERT_EQ(static_cast<size_t>(1), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(1), profilerInfo.InspectedClassCount);
}

TEST(InlineVTCacheTest, ModuleUnloadDropsEverythingBeforeTheNextResolution)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());
    ASSERT_EQ(static_cast<size_t>(1), cache.GetPendingTypeCount());

    cache.OnModuleUnloaded();

    // there is no telling which ClassIDs the unloaded module defined, so none is used
    ASSERT_EQ(static_cast<size_t>(0), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(0), cache.GetPendingTypeCount());
    ASSERT_EQ(static_cast<size_t>(0), cache.GetCachedTypeCount());
    ASSERT_EQ(static_cast<size_t>(0), profilerInfo.InspectedClassCount);
}

TEST(InlineVTCacheTest, TheUnloadNotificationIsConsumedOnceAndOnlyWhenRaised)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);
    cache.GetInlineVTInfo(methodTable.GetClassID());

    // the cache is meant to survive the dumps: without an unload nothing is thrown away
    ASSERT_FALSE(cache.DropCacheIfModuleUnloaded());
    ASSERT_EQ(static_cast<size_t>(1), cache.GetPendingTypeCount());

    cache.OnModuleUnloaded();

    ASSERT_TRUE(cache.DropCacheIfModuleUnloaded());
    ASSERT_FALSE(cache.DropCacheIfModuleUnloaded());
}

TEST(InlineVTCacheTest, NothingIsInspectedWithoutTraversal)
{
    CoreLibMockProfilerInfo profilerInfo;

    auto cache = CreateCache(profilerInfo, nullptr);

    ASSERT_EQ(static_cast<size_t>(0), cache.ResolvePendingTypes());
    ASSERT_EQ(static_cast<size_t>(0), profilerInfo.InspectedClassCount);
}

TEST(InlineVTCacheTest, ClearDropsTheQueuedTypes)
{
    CoreLibMockProfilerInfo profilerInfo;
    FakeMethodTable methodTable;

    auto cache = CreateCache(profilerInfo, nullptr);

    cache.GetInlineVTInfo(methodTable.GetClassID());
    cache.Clear();

    ASSERT_EQ(static_cast<size_t>(0), cache.GetPendingTypeCount());
    ASSERT_EQ(static_cast<size_t>(0), cache.ResolvePendingTypes());
}
