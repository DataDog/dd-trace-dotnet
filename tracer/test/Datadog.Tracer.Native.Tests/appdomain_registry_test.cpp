#include "pch.h"

#include "../../src/Datadog.Tracer.Native/appdomain_registry.h"

using namespace trace;

TEST(AppDomainRegistryTests, AddContainsRemove)
{
    AppDomainRegistry registry;
    AppDomainID id1 = 100;

    EXPECT_FALSE(registry.Contains(id1));
    registry.Add(id1);
    EXPECT_TRUE(registry.Contains(id1));
    EXPECT_EQ(1u, registry.Size());

    const auto removed = registry.Remove(id1);
    EXPECT_EQ(1u, removed);
    EXPECT_FALSE(registry.Contains(id1));
    EXPECT_EQ(0u, registry.Size());
}
