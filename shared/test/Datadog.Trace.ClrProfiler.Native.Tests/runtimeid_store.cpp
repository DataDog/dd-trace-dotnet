#include "gtest/gtest.h"

#include "../../src/Datadog.Trace.ClrProfiler.Native/runtimeid_store.h"

using namespace datadog::shared::nativeloader;

TEST(runtimeid_store, EnsureRuntimeIdIsNotEmptyAtFirstCall)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    auto const& rid = store.Get(id);

    ASSERT_FALSE(rid.empty());
}

TEST(runtimeid_store, EnsureRuntimeIdIsTheSameAfterManyCallsToGetForTheSameAppDomain)
{
    AppDomainID id = 42;
    RuntimeIdStore store;
    auto const& rid = store.Get(id);

    auto const& rid2 = store.Get(id);

    ASSERT_FALSE(rid.empty());
    ASSERT_FALSE(rid2.empty());
    ASSERT_EQ(rid, rid2);

    auto const& rid3 = store.Get(id);

    ASSERT_EQ(rid, rid3);
}

TEST(runtimeid_store, EnsureRuntimeIsDifferentFor2DifferentAppDomains)
{
    AppDomainID appId1 = 42;
    AppDomainID appId2 = 21;

    RuntimeIdStore store;
    auto const& rid = store.Get(appId1);

    auto const& rid2 = store.Get(appId2);

    ASSERT_FALSE(rid.empty());
    ASSERT_FALSE(rid2.empty());
    ASSERT_NE(rid, rid2);
}