#include "pch.h"

#include "../../src/Datadog.Tracer.Native/module_registry.h"

using namespace trace;

TEST(ModuleRegistryTests, AddContainsRemove)
{
    ModuleRegistry registry;
    ModuleID id1 = 1;
    ModuleState state1(10, false, false);

    registry.Add(id1, state1);
    EXPECT_TRUE(registry.Contains(id1));
    EXPECT_EQ(1u, registry.Size());

    registry.Remove(id1);
    EXPECT_FALSE(registry.Contains(id1));
    EXPECT_EQ(0u, registry.Size());
}

TEST(ModuleRegistryTests, TrackStateDoesNotAffectMembership)
{
    ModuleRegistry registry;
    ModuleID id1 = 42;
    ModuleState state1(7, true, false);

    registry.TrackState(id1, state1);
    EXPECT_FALSE(registry.Contains(id1));

    auto tracked = registry.TryGet(id1);
    ASSERT_NE(tracked, nullptr);
    EXPECT_EQ(7u, tracked->AppDomainId());
    EXPECT_TRUE(tracked->IsInternal());
}

TEST(ModuleRegistryTests, SnapshotContainsAddedModules)
{
    ModuleRegistry registry;
    registry.Add(1, ModuleState());
    registry.Add(2, ModuleState());

    const auto snapshot = registry.Snapshot();
    EXPECT_EQ(2u, snapshot.size());
    EXPECT_NE(std::find(snapshot.begin(), snapshot.end(), 1), snapshot.end());
    EXPECT_NE(std::find(snapshot.begin(), snapshot.end(), 2), snapshot.end());
}

TEST(ModuleRegistryTests, TrackStatePreservesInternalFlag)
{
    ModuleRegistry registry;
    ModuleID id1 = 7;

    registry.TrackState(id1, ModuleState(0, true, false));
    registry.TrackState(id1, ModuleState(0, false, false));

    auto tracked = registry.TryGet(id1);
    ASSERT_NE(tracked, nullptr);
    EXPECT_TRUE(tracked->IsInternal());
}
